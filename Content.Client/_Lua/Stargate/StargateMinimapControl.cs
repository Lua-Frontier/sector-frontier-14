// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.
using Content.Shared._Lua.Stargate;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using System;
using System.Collections.Generic;
using System.Numerics;
namespace Content.Client._Lua.Stargate;
public sealed class StargateMinimapControl : Control
{
    [Dependency] private readonly IResourceCache _cache = default!;
    private Dictionary<Vector2i, uint[]> _chunks = new();
    private List<StargateMinimapMarker> _markers = new();
    private List<Vector2> _questZones = new();
    private Vector2? _gatePosition;
    private Vector2? _playerPosition;
    private bool _isStargateWorld;
    private bool _hasDisk;
    private float _viewRange = 64f;
    private Font? _font;
    public Action<Vector2>? OnMapClick;
    public Action<int>? OnMarkerRemove;
    public StargateMinimapControl()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Stop;
        HorizontalExpand = true;
        VerticalExpand = true;
    }
    private Font GetFont() { return _font ??= new VectorFont(_cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 10); }
    public void UpdateState(StargateMinimapUiState state)
    {
        _isStargateWorld = state.IsStargateWorld;
        _hasDisk = state.HasDisk1;
        _chunks = state.ExploredChunks;
        _markers = state.Markers;
        _gatePosition = state.GatePosition;
        _playerPosition = state.PlayerPosition;
        _questZones = state.QuestTargetZones;
    }
    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        handle.DrawRect(new UIBox2(Vector2.Zero, PixelSize), Color.FromHex("#080808"));
        if (!_isStargateWorld) { DrawCentered(handle, Loc.GetString("stargate-minimap-not-planet")); return; }
        if (!_hasDisk || _chunks.Count == 0) { DrawCentered(handle, Loc.GetString("stargate-minimap-insert-disk")); return; }
        if (_playerPosition == null) return;
        var center = PixelSize / 2f;
        var minDim = MathF.Min(PixelSize.X, PixelSize.Y);
        if (minDim < 1f) return;
        var scale = minDim / (_viewRange * 2f);
        var halfViewX = PixelSize.X / (2f * scale);
        var halfViewY = PixelSize.Y / (2f * scale);
        var pp = _playerPosition.Value;
        DrawTiles(handle, center, scale, pp, halfViewX, halfViewY);
        DrawQuestZones(handle, center, scale, pp);
        DrawGateIndicator(handle, center, scale, pp);
        DrawMarkers(handle, center, scale, pp);
        DrawPlayer(handle, center);
    }
    private void DrawTiles(DrawingHandleScreen handle, Vector2 center, float scale, Vector2 pp, float halfViewX, float halfViewY)
    {
        var cs = StargateMinimapConstants.ChunkSize;
        foreach (var (ci, tiles) in _chunks)
        {
            var cb = new Vector2i(ci.X * cs, ci.Y * cs);
            for (var i = 0; i < tiles.Length && i < StargateMinimapConstants.ChunkTileCount; i++)
            {
                if (tiles[i] == 0) continue;
                var lx = i % cs;
                var ly = i / cs;
                var tp = cb + new Vector2i(lx, ly);
                var rel = new Vector2(tp.X + 0.5f, tp.Y + 0.5f) - pp;
                if (MathF.Abs(rel.X) > halfViewX || MathF.Abs(rel.Y) > halfViewY) continue;
                var color = StargateMinimapConstants.UnpackColor(tiles[i]);
                var sp = center + new Vector2(rel.X * scale, -rel.Y * scale);
                var h = MathF.Max(scale * 0.5f, 0.5f);
                handle.DrawRect(new UIBox2(sp.X - h, sp.Y - h, sp.X + h, sp.Y + h), color);
            }
        }
    }
    private void DrawQuestZones(DrawingHandleScreen handle, Vector2 center, float scale, Vector2 pp)
    {
        if (_questZones.Count == 0)
            return;

        var zoneRadius = 21f * scale;
        var font = GetFont();
        var zoneColor = Color.FromHex("#ff444430");
        var ringColor = Color.FromHex("#ff444488");

        for (var i = 0; i < _questZones.Count; i++)
        {
            var rel = _questZones[i] - pp;
            var sp = center + new Vector2(rel.X * scale, -rel.Y * scale);

            if (sp.X + zoneRadius < 0 || sp.Y + zoneRadius < 0 ||
                sp.X - zoneRadius > PixelSize.X || sp.Y - zoneRadius > PixelSize.Y)
                continue;

            handle.DrawCircle(sp, zoneRadius, zoneColor, true);
            handle.DrawCircle(sp, zoneRadius, ringColor, false);

            var label = $"?";
            handle.DrawString(font, sp + new Vector2(-3f, -5f), label, Color.FromHex("#ff6666"));
        }
    }

    private void DrawGateIndicator(DrawingHandleScreen handle, Vector2 center, float scale, Vector2 pp)
    {
        if (_gatePosition == null) return;
        var rel = _gatePosition.Value - pp;
        var sp = center + new Vector2(rel.X * scale, -rel.Y * scale);
        var halfW = PixelSize.X / 2f;
        var halfH = PixelSize.Y / 2f;
        var margin = 20f;
        var inView = MathF.Abs(sp.X - center.X) < halfW - margin && MathF.Abs(sp.Y - center.Y) < halfH - margin;
        if (inView)
        {
            handle.DrawCircle(sp, 7f, Color.FromHex("#0066cc"), true);
            handle.DrawCircle(sp, 7f, Color.White, false);
            handle.DrawCircle(sp, 3f, Color.FromHex("#00aaff"), true);
            var font = GetFont();
            handle.DrawString(font, sp + new Vector2(9f, -5f), Loc.GetString("stargate-minimap-gate"), Color.FromHex("#00aaff"));
            return;
        }
        var dir = new Vector2(sp.X - center.X, sp.Y - center.Y);
        var len = dir.Length();
        if (len < 1f) return;
        dir /= len;
        var maxX = halfW - margin;
        var maxY = halfH - margin;
        var tX = MathF.Abs(dir.X) > 0.001f ? maxX / MathF.Abs(dir.X) : float.MaxValue;
        var tY = MathF.Abs(dir.Y) > 0.001f ? maxY / MathF.Abs(dir.Y) : float.MaxValue;
        var t = MathF.Min(tX, tY);
        var edgePos = center + dir * t;
        var arrowColor = Color.FromHex("#00aaff");
        handle.DrawCircle(edgePos, 6f, arrowColor, true);
        var perp = new Vector2(-dir.Y, dir.X);
        var tip = edgePos + dir * 10f;
        var baseL = edgePos - dir * 4f + perp * 6f;
        var baseR = edgePos - dir * 4f - perp * 6f;
        DrawTriangle(handle, tip, baseL, baseR, arrowColor);
        var font2 = GetFont();
        var dist = MathF.Round(new Vector2(rel.X, rel.Y).Length());
        handle.DrawString(font2, edgePos + new Vector2(8f, -5f), $"{dist}m", arrowColor);
    }
    private static void DrawTriangle(DrawingHandleScreen handle, Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        handle.DrawLine(a, b, color);
        handle.DrawLine(b, c, color);
        handle.DrawLine(c, a, color);
    }
    private void DrawMarkers(DrawingHandleScreen handle, Vector2 center, float scale, Vector2 pp)
    {
        var font = GetFont();
        for (var i = 0; i < _markers.Count; i++)
        {
            var m = _markers[i];
            var rel = m.Position - pp;
            var sp = center + new Vector2(rel.X * scale, -rel.Y * scale);
            if (sp.X < -10 || sp.Y < -10 || sp.X > PixelSize.X + 10 || sp.Y > PixelSize.Y + 10) continue;
            handle.DrawCircle(sp, 5f, Color.FromHex("#ff4444"), true);
            handle.DrawCircle(sp, 5f, Color.White, false);
            if (m.Label != null) handle.DrawString(font, sp + new Vector2(7f, -5f), m.Label, Color.White);
        }
    }
    private void DrawPlayer(DrawingHandleScreen handle, Vector2 center)
    {
        handle.DrawCircle(center, 5f, Color.FromHex("#00ff00"), true);
        handle.DrawCircle(center, 5f, Color.Black, false);
    }
    private void DrawCentered(DrawingHandleScreen handle, string text)
    {
        var font = GetFont();
        var pos = PixelSize / 2f - new Vector2(text.Length * 3f, 5f);
        handle.DrawString(font, pos, text, Color.FromHex("#666666"));
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);
        _viewRange = Math.Clamp(_viewRange + (args.Delta.Y > 0 ? -8f : 8f), 16f, 256f);
        args.Handle();
    }
    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (_playerPosition == null) return;
        var lp = args.PointerLocation.Position - GlobalPixelPosition;
        var center = PixelSize / 2f;
        var minDim = MathF.Min(PixelSize.X, PixelSize.Y);
        var scale = minDim / (_viewRange * 2f);
        if (scale <= 0f) return;
        var d = lp - center;
        var worldPos = _playerPosition.Value + new Vector2(d.X / scale, -d.Y / scale);
        if (args.Function == EngineKeyFunctions.UIRightClick)
        {
            var bestIdx = -1;
            var bestDist = 9f;
            for (var i = 0; i < _markers.Count; i++)
            {
                var dist = Vector2.DistanceSquared(_markers[i].Position, worldPos);
                if (dist < bestDist) { bestDist = dist; bestIdx = i; }
            }
            if (bestIdx >= 0) { OnMarkerRemove?.Invoke(bestIdx); args.Handle(); }
            return;
        }
        if (args.Function == EngineKeyFunctions.UIClick) { OnMapClick?.Invoke(worldPos); args.Handle(); }
    }
}
