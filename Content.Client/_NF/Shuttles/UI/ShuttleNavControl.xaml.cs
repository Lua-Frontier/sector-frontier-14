// New Frontiers - This file is licensed under AGPLv3
// Copyright (c) 2024 New Frontiers Contributors
// See AGPLv3.txt for details.
using Content.Shared._NF.Shuttles.Events;
using Content.Shared.Shuttles.BUIStates;
using Robust.Shared.Physics.Components;
using System.Numerics;
using Content.Shared._Mono.Company;
using Content.Shared.Shuttles.Components;
using Robust.Client.Graphics;
using Robust.Shared.Collections;
using Robust.Shared.Prototypes;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Timing;
using Content.Shared._Mono.Radar;
using Content.Client._Mono.Radar;
using Content.Client.Station;

// Purposefully colliding with base namespace.
namespace Content.Client.Shuttles.UI;

public partial class ShuttleNavControl // Mono
{
    // Dependency
    private readonly StationSystem _station;
    private readonly RadarBlipsSystem _blips;

    // Constants for gunnery system
    // These 2 handle timing updates
    private static readonly Color TargetColor = Color.FromHex("#99ff66");

    #region Mono
    // These 2 handle timing updates
    private const float RadarUpdateInterval = 0f;
    private float _updateAccumulator = 0f;

    /// <summary>
    /// Whether the shuttle is currently in FTL. This is used to disable the Park button
    /// while in FTL to prevent parking while traveling.
    /// </summary>
    public bool InFtl { get; set; }
    #endregion

    protected bool _isMouseDown;
    protected bool _isMouseInside;
    protected Vector2 _lastMousePos;
    protected float _lastFireTime;
    protected const float FireRateLimit = 0.1f; // 100ms between shots

    // Constants for IFF system
    public float MaximumIFFDistance { get; set; } = -1f;
    public bool HideCoords { get; set; } = false;
    private static Color _dockLabelColor = Color.White;
    public bool HideTarget { get; set; } = false;
    public Vector2? Target { get; set; } = null;
    public NetEntity? TargetEntity { get; set; } = null;

    public InertiaDampeningMode DampeningMode { get; set; }
    public ServiceFlags ServiceFlags { get; set; } = ServiceFlags.None;

    /// <summary>
    /// Updates the radar UI with the latest navigation state and sets additional NF-specific state.
    /// </summary>
    /// <param name="state">The navigation interface state.</param>
    private void NFUpdateState(NavInterfaceState state)
    {
        if (state.MaxIffRange != null)
            MaximumIFFDistance = state.MaxIffRange.Value;
        HideCoords = state.HideCoords;
        Target = state.Target;
        TargetEntity = state.TargetEntity;
        HideTarget = state.HideTarget;

        if (!EntManager.GetCoordinates(state.Coordinates).HasValue ||
            !EntManager.TryGetComponent(EntManager.GetCoordinates(state.Coordinates).GetValueOrDefault().EntityId, out TransformComponent? transform) ||
                !EntManager.TryGetComponent(transform.GridUid, out PhysicsComponent? physicsComponent))
        {
            return;
        }

        DampeningMode = state.DampeningMode;
        ServiceFlags = state.ServiceFlags;

        // Check if the entity has an FTLComponent which indicates it's in FTL
        if (transform.GridUid != null)
        {
            InFtl = EntManager.HasComponent<FTLComponent>(transform.GridUid);
        }
        else
        {
            InFtl = false;
        }
    }

    /// <summary>
    /// Checks if an IFF marker should be drawn based on distance and maximum IFF range.
    /// </summary>
    /// <param name="shouldDrawIff">Whether the IFF marker would otherwise be drawn.</param>
    /// <param name="distance">The distance vector to the object.</param>
    /// <returns>True if the IFF marker should be drawn, false otherwise.</returns>
    private bool NFCheckShouldDrawIffRangeCondition(bool shouldDrawIff, Vector2 distance)
    {
        if (shouldDrawIff && MaximumIFFDistance >= 0.0f)
        {
            if (distance.Length() > MaximumIFFDistance)
            {
                shouldDrawIff = false;
            }
        }

        return shouldDrawIff;
    }

    /// <summary>
    /// Adds a blip to the blip data list for later drawing.
    /// </summary>
    private static void NFAddBlipToList(List<BlipData> blipDataList, bool isOutsideRadarCircle, Vector2 uiPosition, int uiXCentre, int uiYCentre, Color color, float scale = 1f, EntityUid gridUid = default)
    {
        // Check if the entity has a company component and use that color if available
        Color blipColor = color;

        if (gridUid != default &&
            IoCManager.Resolve<IEntityManager>().TryGetComponent(gridUid, out Shared._Mono.Company.CompanyComponent? companyComp) &&
            !string.IsNullOrEmpty(companyComp.CompanyName))
        {
            var prototypeManager = IoCManager.Resolve<IPrototypeManager>();
            if (prototypeManager.TryIndex<CompanyPrototype>(companyComp.CompanyName, out var prototype) && prototype != null)
            {
                blipColor = prototype.Color;
            }
        }

        blipDataList.Add(new BlipData
        {
            IsOutsideRadarCircle = isOutsideRadarCircle,
            UiPosition = uiPosition,
            VectorToPosition = uiPosition - new Vector2(uiXCentre, uiYCentre),
            Color = blipColor,
            Scale = scale
        });
    }

    /// <summary>
    /// Adds blip style triangles that are on ships or pointing towards ships on the edges of the radar.
    /// Draws blips at the BlipData's uiPosition and uses VectorToPosition to rotate to point towards ships.
    /// </summary>
    private void NFDrawBlips(DrawingHandleBase handle, List<BlipData> blipDataList)
    {
        var blipValueList = new Dictionary<Color, ValueList<Vector2>>();

        foreach (var blipData in blipDataList)
        {
            var s = blipData.Scale <= 0f ? 1f : blipData.Scale; // Lua mod icon
            var triangleShapeVectorPoints = new[]
            {
                new Vector2(0, 0),
                new Vector2(RadarBlipSize * s, 0), // Lua mod icon
                new Vector2(RadarBlipSize * 0.5f * s, RadarBlipSize * s)  // Lua mod icon
            };

            if (blipData.IsOutsideRadarCircle)
            {
                // Calculate the angle of rotation
                var angle = (float)Math.Atan2(blipData.VectorToPosition.Y, blipData.VectorToPosition.X) + -1.6f;

                // Manually create a rotation matrix
                var cos = (float)Math.Cos(angle);
                var sin = (float)Math.Sin(angle);
                float[,] rotationMatrix = { { cos, -sin }, { sin, cos } };

                // Rotate each vertex
                for (var i = 0; i < triangleShapeVectorPoints.Length; i++)
                {
                    var vertex = triangleShapeVectorPoints[i];
                    var x = vertex.X * rotationMatrix[0, 0] + vertex.Y * rotationMatrix[0, 1];
                    var y = vertex.X * rotationMatrix[1, 0] + vertex.Y * rotationMatrix[1, 1];
                    triangleShapeVectorPoints[i] = new Vector2(x, y);
                }
            }

            var triangleCenterVector =
                (triangleShapeVectorPoints[0] + triangleShapeVectorPoints[1] + triangleShapeVectorPoints[2]) / 3;

            // Calculate the vectors from the center to each vertex
            var vectorsFromCenter = new Vector2[3];
            for (int i = 0; i < 3; i++)
            {
                vectorsFromCenter[i] = (triangleShapeVectorPoints[i] - triangleCenterVector) * UIScale;
            }

            // Calculate the vertices of the new triangle
            var newVerts = new Vector2[3];
            for (var i = 0; i < 3; i++)
            {
                newVerts[i] = (blipData.UiPosition * UIScale) + vectorsFromCenter[i];
            }

            if (!blipValueList.TryGetValue(blipData.Color, out var valueList))
            {
                valueList = new ValueList<Vector2>();

            }
            valueList.Add(newVerts[0]);
            valueList.Add(newVerts[1]);
            valueList.Add(newVerts[2]);
            blipValueList[blipData.Color] = valueList;
        }

        // One draw call for every color we have
        foreach (var color in blipValueList)
        {
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, color.Value.Span, color.Key);
        }
    }

    private void HandleMouseEntered(GUIMouseHoverEventArgs args)
    {
        _isMouseInside = true;
    }

    private void HandleMouseExited(GUIMouseHoverEventArgs args)
    {
        _isMouseInside = false;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _isMouseDown = true;
        _lastMousePos = args.RelativePosition;
        TryFireAtPosition(args.RelativePosition);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        _updateAccumulator += args.DeltaSeconds;

        if (_updateAccumulator >= RadarUpdateInterval)
        {
            _updateAccumulator = 0; // I'm not subtracting because frame updates can majorly lag in a way normal ones cannot.

            if (_consoleEntity != null)
                _blips.RequestBlips((EntityUid)_consoleEntity);
        }

        if (_isMouseDown && _isMouseInside)
        {
            var currentTime = IoCManager.Resolve<IGameTiming>().CurTime.TotalSeconds;
            if (currentTime - _lastFireTime >= FireRateLimit)
            {
                var mousePos = UserInterfaceManager.MousePositionScaled;
                var relativePos = mousePos.Position - GlobalPosition;
                if (relativePos != _lastMousePos)
                {
                    _lastMousePos = relativePos;
                }
                TryFireAtPosition(relativePos);
                _lastFireTime = (float)currentTime;
            }
        }
    }

    private void TryFireAtPosition(Vector2 relativePosition)
    {
        if (_coordinates == null || _rotation == null || OnRadarClick == null)
            return;

        var a = InverseScalePosition(relativePosition);
        var relativeWorldPos = new Vector2(a.X, -a.Y);
        relativeWorldPos = _rotation.Value.RotateVec(relativeWorldPos);
        var coords = _coordinates.Value.Offset(relativeWorldPos);
        OnRadarClick?.Invoke(coords);
    }

    private void DrawBlipShape(DrawingHandleScreen handle, Vector2 position, float size, Color color, RadarBlipShape shape)
    {
        switch (shape)
        {
            case RadarBlipShape.Circle:
                handle.DrawCircle(position, size, color);
                break;
            case RadarBlipShape.Square:
                var halfSize = size / 2;
                var rect = new UIBox2(
                    position.X - halfSize,
                    position.Y - halfSize,
                    position.X + halfSize,
                    position.Y + halfSize
                );
                handle.DrawRect(rect, color);
                break;
            case RadarBlipShape.Triangle:
                var points = new Vector2[]
                {
                    position + new Vector2(0, -size),
                    position + new Vector2(-size * 0.866f, size * 0.5f),
                    position + new Vector2(size * 0.866f, size * 0.5f)
                };
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, points, color);
                break;
            case RadarBlipShape.Star:
                DrawStar(handle, position, size, color);
                break;
            case RadarBlipShape.Diamond:
                var diamondPoints = new Vector2[]
                {
                    position + new Vector2(0, -size),
                    position + new Vector2(size, 0),
                    position + new Vector2(0, size),
                    position + new Vector2(-size, 0)
                };
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, diamondPoints, color);
                break;
            case RadarBlipShape.Hexagon:
                DrawHexagon(handle, position, size, color);
                break;
            case RadarBlipShape.Arrow:
                DrawArrow(handle, position, size, color);
                break;
        }
    }

    private void DrawStar(DrawingHandleScreen handle, Vector2 position, float size, Color color)
    {
        const int points = 5;
        const float innerRatio = 0.4f;
        var vertices = new Vector2[points * 2];

        for (var i = 0; i < points * 2; i++)
        {
            var angle = i * Math.PI / points;
            var radius = i % 2 == 0 ? size : size * innerRatio;
            vertices[i] = position + new Vector2(
                (float)Math.Sin(angle) * radius,
                -(float)Math.Cos(angle) * radius
            );
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, vertices, color);
    }

    private void DrawHexagon(DrawingHandleScreen handle, Vector2 position, float size, Color color)
    {
        var vertices = new Vector2[6];
        for (var i = 0; i < 6; i++)
        {
            var angle = i * Math.PI / 3;
            vertices[i] = position + new Vector2(
                (float)Math.Sin(angle) * size,
                -(float)Math.Cos(angle) * size
            );
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, vertices, color);
    }

    private void DrawArrow(DrawingHandleScreen handle, Vector2 position, float size, Color color)
    {
        var vertices = new Vector2[]
        {
            position + new Vector2(0, -size),           // Tip
            position + new Vector2(-size * 0.5f, 0),    // Left wing
            position + new Vector2(0, size * 0.5f),     // Bottom
            position + new Vector2(size * 0.5f, 0)      // Right wing
        };

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, vertices, color);
    }
}
