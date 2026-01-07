// LuaWorld/LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld/LuaCorp
// See AGPLv3.txt for details.

using Content.Client.Shuttles.UI;
using Content.Shared.Shuttles.BUIStates;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Map;
using System.Numerics;

namespace Content.Client._Lua.Shipyard.UI;

public sealed class ShipyardDockRadarControl : ShuttleNavControl
{
    [Dependency] private readonly IPlayerManager _player = default!;

    private readonly SharedTransformSystem _transform;
    private static readonly Color ShipyardTileColor = Color.FromHex("#1a1a1a");
    private static readonly Color ShipyardWallColor = Color.FromHex("#404040");
    private static readonly Color ShipyardTileColorSrgb = Color.ToSrgb(ShipyardTileColor);
    private static readonly Color ShipyardWallColorSrgb = Color.ToSrgb(ShipyardWallColor);
    protected override Color RadarEquatorialLineColor => ShipyardWallColorSrgb;
    protected override Color RadarRadialLineColor => ShipyardWallColorSrgb;
    protected override bool AllowResize => true;
    protected override bool ScaleWithControlSize => true;
    private EntityCoordinates? _baseCoords;
    private Angle? _baseAngle;
    private Vector2 _pan;
    private bool _panning;
    private bool _mouseDown;
    private Vector2 _mouseDownPos;
    private float _dragAccumulatedPx;
    private const float PanClamp = 150f;
    private const float DragThresholdPx = 6f;
    private const float MinZoomRange = 3f;

    public ShipyardDockRadarControl() : base()
    {
        IoCManager.InjectDependencies(this);
        _transform = EntManager.System<SharedTransformSystem>();
    }

    protected override void ModifyGridPalette(ref Color fillColor, ref Color edgeColor, ref float fillAlpha, EntityUid gridUid, bool self)
    {
        fillColor = ShipyardTileColorSrgb;
        edgeColor = ShipyardWallColorSrgb;
        fillAlpha = 0.9f;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        const string text = "Выберите стыковочный порт";
        var padding = 6f * UIScale;
        var dims = handle.GetDimensions(Font, text, UIScale);
        var pos = new Vector2(PixelWidth - dims.X - padding, padding);
        handle.DrawString(Font, pos, text, UIScale, Color.White.WithAlpha(0.9f));
        DrawLocalPlayerMarker(handle);
    }

    private void DrawLocalPlayerMarker(DrawingHandleScreen handle)
    {
        if (_coordinates == null || _rotation == null) return;
        var playerEnt = _player.LocalSession?.AttachedEntity;
        if (playerEnt == null) return;
        var xformQuery = EntManager.GetEntityQuery<TransformComponent>();
        if (!xformQuery.TryGetComponent(_coordinates.Value.EntityId, out var anchorXform) || anchorXform.MapID == MapId.Nullspace) { return; }
        if (!xformQuery.TryGetComponent(playerEnt.Value, out var playerXform) || playerXform.MapID != anchorXform.MapID) { return; }
        var posMatrix = Matrix3Helpers.CreateTransform(_coordinates.Value.Position, _rotation.Value);
        var ourEntRot = RotateWithEntity ? _transform.GetWorldRotation(anchorXform) : _rotation.Value;
        var ourEntMatrix = Matrix3Helpers.CreateTransform(_transform.GetWorldPosition(anchorXform), ourEntRot);
        var shuttleToWorld = Matrix3x2.Multiply(posMatrix, ourEntMatrix);
        Matrix3x2.Invert(shuttleToWorld, out var worldToShuttle);
        var shuttleToView = Matrix3x2.CreateScale(new Vector2(MinimapScale, -MinimapScale)) * Matrix3x2.CreateTranslation(MidPointVector);
        var playerWorldPos = _transform.GetWorldPosition(playerEnt.Value);
        var p = Vector2.Transform(playerWorldPos, worldToShuttle * shuttleToView);
        const float radius = 5f;
        var fill = Color.ToSrgb(Color.Cyan).WithAlpha(0.9f);
        var outline = Color.Black.WithAlpha(0.8f);
        handle.DrawCircle(p, radius, fill, filled: true);
        handle.DrawCircle(p, radius, outline, filled: false);
    }

    public new void UpdateState(NavInterfaceState state)
    {
        WorldMinRange = Math.Min(WorldMinRange, MinZoomRange);
        base.UpdateState(state);
        _baseCoords = EntManager.GetCoordinates(state.Coordinates);
        _baseAngle = state.Angle;
        ApplyPan();
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        if (args.Function == EngineKeyFunctions.UIClick)
        {
            _mouseDown = true;
            _mouseDownPos = args.PointerLocation.Position;
            _panning = false;
            _dragAccumulatedPx = 0f;
            return;
        }
        base.KeyBindDown(args);
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        if (args.Function == EngineKeyFunctions.UIClick)
        {
            _mouseDown = false;
            if (_panning)
            {
                _panning = false;
                args.Handle();
                return;
            }
            base.KeyBindUp(args);
            return;
        }
        base.KeyBindUp(args);
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (_baseCoords == null || _baseAngle == null) return;
        if (_mouseDown && !_panning)
        {
            _dragAccumulatedPx += new Vector2(args.Relative.X, args.Relative.Y).Length();
            if (_dragAccumulatedPx >= DragThresholdPx) _panning = true;
        }
        if (!_panning) return;
        if (MidPoint <= 0) return;
        var delta = new Vector2(args.Relative.X, -args.Relative.Y) / MidPoint * WorldRange;
        delta = _baseAngle.Value.RotateVec(delta);
        _pan -= delta;
        _pan = new Vector2( Math.Clamp(_pan.X, -PanClamp, PanClamp), Math.Clamp(_pan.Y, -PanClamp, PanClamp));
        ApplyPan();
    }

    private void ApplyPan()
    {
        if (_baseCoords == null) return;
        Offset = Vector2.Zero;
        TargetOffset = Vector2.Zero;
        SetMatrix(_baseCoords.Value.Offset(_pan), _baseAngle);
    }
}


