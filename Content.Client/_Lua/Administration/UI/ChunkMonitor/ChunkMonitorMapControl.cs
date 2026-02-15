// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaCorp
// See AGPLv3.txt for details.
using Content.Client.UserInterface.Controls;
using Content.Shared._Lua.Administration.ChunkMonitor;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Collections;
using Robust.Shared.Input;
using System.Numerics;
using Robust.Client.Player;

namespace Content.Client._Lua.Administration.UI.ChunkMonitor;

public enum ChunkMonitorHeatmapMode : byte
{
    Off = 0,
    Entities = 1,
    Players = 2,
    Shuttles = 3,
    Debris = 4,
    Grids = 5,
}

public sealed class ChunkMonitorMapControl : MapGridControl
{
    private const float ChunkSize = 128f;
    [Dependency] private readonly IPlayerManager _player = default!;
    protected override bool Draggable => true;
    public event Action<Vector2i>? ChunkClicked;
    public Vector2i? Selected;
    private EntityUid? _selectedMapUid;
    private readonly Dictionary<Vector2i, ChunkMonitorChunkInfo> _chunks = new();
    private ChunkMonitorHeatmapMode _heatmapMode = ChunkMonitorHeatmapMode.Off;
    private int _maxEntities;
    private int _maxPlayers;
    private int _maxShuttles;
    private int _maxDebris;
    private int _maxGrids;

    public ChunkMonitorMapControl() : base(minRange: 8f, maxRange: 256f, range: 48f)
    {
        IoCManager.InjectDependencies(this);
    }

    public void SetSelectedMap(NetEntity map)
    {
        if (!EntManager.TryGetEntity(map, out EntityUid? uid))
        {
            _selectedMapUid = null;
            return;
        }
        _selectedMapUid = uid;
    }

    public void SetChunks(ChunkMonitorChunkInfo[] chunks)
    {
        _chunks.Clear();
        _maxEntities = 0;
        _maxPlayers = 0;
        _maxShuttles = 0;
        _maxDebris = 0;
        _maxGrids = 0;
        foreach (var chunk in chunks)
        {
            _chunks[chunk.Coordinates] = chunk;
            if (chunk.EntityCount > _maxEntities) _maxEntities = chunk.EntityCount;
            if (chunk.PlayerCount > _maxPlayers) _maxPlayers = chunk.PlayerCount;
            if (chunk.ShuttleCount > _maxShuttles) _maxShuttles = chunk.ShuttleCount;
            if (chunk.DebrisCount > _maxDebris) _maxDebris = chunk.DebrisCount;
            if (chunk.GridCount > _maxGrids) _maxGrids = chunk.GridCount;
        }
    }

    public void SetHeatmapMode(ChunkMonitorHeatmapMode mode)
    {
        _heatmapMode = mode;
    }

    public bool TryGetChunk(Vector2i coords, out ChunkMonitorChunkInfo info)
        => _chunks.TryGetValue(coords, out info);

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;
        if ((StartDragPosition - args.PointerLocation.Position).Length() > 5f)
            return;
        var localPos = args.PointerLocation.Position - GlobalPixelPosition;
        var mapPos = InverseMapPosition(localPos);
        var coords = new Vector2i((int) MathF.Floor(mapPos.X), (int) MathF.Floor(mapPos.Y));
        if (_chunks.ContainsKey(coords))
        {
            Selected = coords;
            ChunkClicked?.Invoke(coords);
            args.Handle();
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        DrawBacking(handle);
        DrawRecenter();
        var matty = Matrix3Helpers.CreateInverseTransform(Offset, Angle.Zero);
        var min = Offset - WorldRangeVector;
        var max = Offset + WorldRangeVector;
        var minX = (int) MathF.Floor(min.X) - 1;
        var maxX = (int) MathF.Ceiling(max.X) + 1;
        var minY = (int) MathF.Floor(min.Y) - 1;
        var maxY = (int) MathF.Ceiling(max.Y) + 1;
        var loadedFill = new Color(0.20f, 0.75f, 0.25f, 0.70f);
        var unloadedFill = new Color(0.55f, 0.55f, 0.55f, 0.55f);
        var loadedBorder = new Color(0.15f, 0.65f, 0.20f, 1.00f);
        var unloadedBorder = new Color(0.35f, 0.35f, 0.35f, 1.00f);
        var selectedOutline = new Color(1f, 0.85f, 0.35f, 1f);
        foreach (var (coords, chunk) in _chunks)
        {
            if (coords.X < minX || coords.X > maxX || coords.Y < minY || coords.Y > maxY)
                continue;
            var border = chunk.Status == ChunkMonitorChunkStatus.Loaded ? loadedBorder : unloadedBorder;
            var fill = GetFillColor(chunk, loadedFill, unloadedFill);
            var lt = WorldToUi(new Vector2(coords.X, coords.Y + 1), matty);
            var rb = WorldToUi(new Vector2(coords.X + 1, coords.Y), matty);
            var box = new UIBox2(lt, rb);
            handle.DrawRect(box, fill);
            handle.DrawRect(box, border, filled: false);
            if (Selected != null && Selected.Value == coords)
            {
                var outline = new UIBox2(
                    box.TopLeft - new Vector2(1f, 1f),
                    box.BottomRight + new Vector2(1f, 1f));
                handle.DrawRect(outline, selectedOutline, filled: false);
            }
        }
        var gridLineColor = new Color(0.0f, 0.0f, 0.0f, 0.55f);
        var linePts = new ValueList<Vector2>(Math.Max(0, (maxX - minX + maxY - minY) * 2));
        for (var x = minX; x <= maxX; x++)
        {
            var a = WorldToUi(new Vector2(x, minY), matty);
            var b = WorldToUi(new Vector2(x, maxY), matty);
            linePts.Add(a);
            linePts.Add(b);
        }
        for (var y = minY; y <= maxY; y++)
        {
            var a = WorldToUi(new Vector2(minX, y), matty);
            var b = WorldToUi(new Vector2(maxX, y), matty);
            linePts.Add(a);
            linePts.Add(b);
        }
        if (linePts.Count > 0)
            handle.DrawPrimitives(DrawPrimitiveTopology.LineList, linePts.Span, gridLineColor);
        DrawLocalPlayerChunkHighlight(handle, matty, minX, maxX, minY, maxY);
    }

    private Color GetFillColor(ChunkMonitorChunkInfo chunk, Color loadedFill, Color unloadedFill)
    {
        if (_heatmapMode == ChunkMonitorHeatmapMode.Off)
        {
            if (chunk.PlayerCount > 0)
                return new Color(0.0f, 0.35f, 0.0f, 0.70f);
            return chunk.Status == ChunkMonitorChunkStatus.Loaded ? loadedFill : unloadedFill;
        }
        var value = _heatmapMode switch
        {
            ChunkMonitorHeatmapMode.Entities => chunk.EntityCount,
            ChunkMonitorHeatmapMode.Players => chunk.PlayerCount,
            ChunkMonitorHeatmapMode.Shuttles => chunk.ShuttleCount,
            ChunkMonitorHeatmapMode.Debris => chunk.DebrisCount,
            ChunkMonitorHeatmapMode.Grids => chunk.GridCount,
            _ => 0
        };
        var max = _heatmapMode switch
        {
            ChunkMonitorHeatmapMode.Entities => _maxEntities,
            ChunkMonitorHeatmapMode.Players => _maxPlayers,
            ChunkMonitorHeatmapMode.Shuttles => _maxShuttles,
            ChunkMonitorHeatmapMode.Debris => _maxDebris,
            ChunkMonitorHeatmapMode.Grids => _maxGrids,
            _ => 0
        };
        return HeatColor(value, max);
    }
    private static Color HeatColor(int value, int max)
    {
        if (max <= 0 || value <= 0)
            return new Color(0.10f, 0.12f, 0.16f, 0.40f);
        var t = Math.Clamp(value / (float) max, 0f, 1f);
        var r = Math.Clamp(2.2f * t - 0.2f, 0f, 1f);
        var g = Math.Clamp(2.2f * (1f - MathF.Abs(t - 0.5f) * 2f), 0f, 1f);
        var b = Math.Clamp(1.1f * (1f - t), 0f, 1f) * 0.45f;
        var a = 0.35f + 0.45f * t;
        return new Color(r, g, b, a);
    }
    private Vector2 WorldToUi(Vector2 world, Matrix3x2 mapTransform)
    {
        var relative = Vector2.Transform(world, mapTransform);
        relative = relative with { Y = -relative.Y };
        return ScalePosition(relative);
    }
    private void DrawLocalPlayerChunkHighlight(DrawingHandleScreen handle, Matrix3x2 mapTransform, int minX, int maxX, int minY, int maxY)
    {
        if (_selectedMapUid == null)
            return;
        var ent = _player.LocalEntity;
        if (ent == null)
            return;
        if (!EntManager.TryGetComponent(ent.Value, out TransformComponent? xform) || xform.MapUid == null)
            return;
        if (xform.MapUid.Value != _selectedMapUid.Value)
            return;
        var xformSys = EntManager.System<SharedTransformSystem>();
        var worldPos = xformSys.GetWorldPosition(xform);
        var chunkX = (int) MathF.Floor(worldPos.X / ChunkSize);
        var chunkY = (int) MathF.Floor(worldPos.Y / ChunkSize);
        if (chunkX < minX || chunkX > maxX || chunkY < minY || chunkY > maxY)
            return;
        var playerColor = new Color(0.0f, 0.35f, 0.0f, 0.55f);
        var playerOutline = new Color(0.0f, 0.55f, 0.0f, 0.95f);
        var lt = WorldToUi(new Vector2(chunkX, chunkY + 1), mapTransform);
        var rb = WorldToUi(new Vector2(chunkX + 1, chunkY), mapTransform);
        var box = new UIBox2(lt, rb);
        handle.DrawRect(box, playerColor);
        handle.DrawRect(box, playerOutline, filled: false);
    }
}
