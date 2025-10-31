// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Shared._Lua.Starmap;
using Content.Shared._Lua.Sectors;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.Client._Lua.Starmap;

public sealed class StarmapControl : Control
{
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    public float Range = 1f;
    public float Zoom { get; private set; } = 1f;
    private List<Star> _stars = new List<Star>();
    private float _basePpd = 90f;
    private readonly Font _font;
    public event Action<Star>? OnStarSelect;
    private Star? _hoveredStar;
    private Vector2 _offsetWorld = Vector2.Zero;
    private bool _isDragging;
    private Vector2 _lastMouseLocal;
    private float _dragAccumulated;
    private readonly HashSet<MapId> _adjacentTargetMaps = new();
    private List<HyperlaneEdge> _edges = new();
    private HashSet<MapId> _visibleSectorMaps = new();
    private Dictionary<MapId, string> _sectorIdByMap = new();
    private Dictionary<MapId, string> _ownerByMap = new();
    private Dictionary<MapId, string> _sectorColorOverrideHexByMap = new();
    private StarmapConfigPrototype? _config;

    public StarmapControl()
    {
        IoCManager.InjectDependencies(this);
        var cache = IoCManager.Resolve<IResourceCache>();
        _font = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 8);
        RectClipContent = true;
        RectDrawClipMargin = 0;
        try
        {
            if (_proto.TryIndex<StarmapConfigPrototype>("StarmapConfig", out var cfg))
            {
                _config = cfg;
                _basePpd = cfg.BasePixelsPerDistance;
            }
        }
        catch { }
    }

    public void SetStars(List<Star> stars)
    { _stars = stars; }

    public void SetEdges(List<HyperlaneEdge> edges)
    { _edges = edges; }

    public void SetVisibleSectorMaps(List<MapId> maps)
    { _visibleSectorMaps = new HashSet<MapId>(maps ?? new List<MapId>()); }

    public void SetSectorIdByMap(Dictionary<MapId, string> map)
    { _sectorIdByMap = map ?? new Dictionary<MapId, string>(); }

    public void SetOwnerByMap(Dictionary<MapId, string> owners)
    { _ownerByMap = owners ?? new Dictionary<MapId, string>(); }

    public void SetSectorColorOverridesHex(Dictionary<MapId, string> overrides)
    { _sectorColorOverrideHexByMap = overrides ?? new Dictionary<MapId, string>(); }

    public bool TryGetOwner(MapId mapId, out string owner)
    { return _ownerByMap.TryGetValue(mapId, out owner!); }

    public bool IsSector(MapId mapId)
    { return _sectorIdByMap.ContainsKey(mapId); }

    private bool TryGetOverrideColor(MapId mapId, out Color color)
    {
        color = default;
        if (_sectorColorOverrideHexByMap == null) return false;
        if (!_sectorColorOverrideHexByMap.TryGetValue(mapId, out var hex) || string.IsNullOrWhiteSpace(hex)) return false;
        try { color = Color.FromHex(hex); return true; } catch { return false; }
    }

    public void SetZoom(float zoom)
    {
        var min = _config?.ZoomMin ?? 0.05f;
        var max = _config?.ZoomMax ?? 4f;
        Zoom = Math.Clamp(zoom, min, max);
        UpdateDraw();
    }

    public void ZoomIn()
    { SetZoom(Zoom + 0.1f); }

    public void ZoomOut()
    { SetZoom(Zoom - 0.1f); }

    private Vector2 CalculateOffsetPx()
    { return PixelSize / 2; }

    private Vector2 GetMouseLocalPx()
    {
        var screenPos = _inputManager.MouseScreenPosition;
        return GetLocalPosition(screenPos);
    }

    private float Ppd => _basePpd * Zoom;

    private Vector2 GetPositionOfStar(Vector2 position)
    { return CalculateOffsetPx() + ((position - _offsetWorld) * Ppd); }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var bg = _config?.BackgroundColor ?? new Color(5, 5, 10, 255);
        handle.DrawRect(new UIBox2(Vector2.Zero, PixelSize), bg);
        DrawParallax(handle);
        var lines = _config?.GridLines ?? 10;
        for (var i = 0; i < lines; i++)
        {
            var xStep = PixelSize.X / lines;
            var yStep = PixelSize.Y / lines;
            var gridColor = _config?.GridColor ?? Color.DarkSlateGray;
            handle.DrawLine(new Vector2(i * xStep, 0), new Vector2(i * xStep, Size.Y), gridColor);
            handle.DrawLine(new Vector2(0, i * yStep), new Vector2(Size.X, i * yStep), gridColor);
        }
        _adjacentTargetMaps.Clear();
        if (_stars.Count > 1 && _edges != null && _edges.Count > 0)
        {
            var centerStarIndex = 0;
            var minDistance = float.MaxValue;
            for (var i = 0; i < _stars.Count; i++)
            {
                var distance = Vector2.Distance(_stars[i].Position, Vector2.Zero);
                if (distance < minDistance)
                { minDistance = distance; centerStarIndex = i; }
            }
            var grey = new Color(112, 128, 144, 120);
            foreach (var e in _edges)
            {
                if (e.A < 0 || e.B < 0 || e.A >= _stars.Count || e.B >= _stars.Count) continue;
                if (!IsStarVisible(_stars[e.A]) || !IsStarVisible(_stars[e.B])) continue;
                var fromPos = GetPositionOfStar(_stars[e.A].Position);
                var toPos = GetPositionOfStar(_stars[e.B].Position);
                handle.DrawLine(fromPos, toPos, grey);
                if (e.A == centerStarIndex) _adjacentTargetMaps.Add(_stars[e.B].Map);
                if (e.B == centerStarIndex) _adjacentTargetMaps.Add(_stars[e.A].Map);
            }
        }
        _hoveredStar = null;
        foreach (var star in _stars)
        {
            if (!IsStarVisible(star)) continue;
            var uiPosition = GetPositionOfStar(star.Position);
            var localMouse = GetMouseLocalPx();
            var radius = 5f;
            var hovered = Vector2.Distance(localMouse, uiPosition) <= radius * 1.5f;
            var color = Color.White;
            var name = star.Name;
            var isSector = IsSectorStar(star.Map);
            if (Vector2.Distance(Vector2.Zero, star.Position) >= Range) color = Color.Red;
            if (Vector2.Distance(Vector2.Zero, star.Position) >= Range * 1.5)
            {
                color = Color.DarkRed;
                name = Loc.GetString("ship-ftl-tag-oor");
            }
            if (star.Position == Vector2.Zero) color = Color.Blue;
            if (isSector)
            {
                color = GetSectorColor(star.Map);
                radius = GetSectorSize(star.Map);
            }
            else if (TryGetOverrideColor(star.Map, out var overrideColor))
            { color = overrideColor; }
            if (hovered) { radius = isSector ? 12f : 10f; }
            if (isSector)
            {
                handle.DrawCircle(uiPosition, radius + 2, color with { A = 100 }, false);
                handle.DrawCircle(uiPosition, radius, color);
                handle.DrawCircle(uiPosition, radius - 2, Color.White with { A = 150 });
            }
            else
            { handle.DrawCircle(uiPosition, radius, color); }
            if (hovered)
            {
                handle.DrawString(_font, uiPosition + new Vector2(10, 0), name);
                _hoveredStar = star;
            }
        }
    }

    private bool IsStarVisible(Star star)
    {
        var isSector = IsSectorStar(star.Map);
        if (!isSector) return true;
        if (star.Position == Vector2.Zero) return true;
        if (_visibleSectorMaps.Count == 0) return false;
        return _visibleSectorMaps.Contains(star.Map);
    }

    private static float Hash01(int x, int y, int seed)
    {
        unchecked
        {
            var h = x * 73856093 ^ y * 19349663 ^ seed * 83492791;
            h &= 0x7fffffff;
            return (h / (float) int.MaxValue);
        }
    }

    private void DrawParallax(DrawingHandleScreen handle)
    {
        if (_config?.ParallaxLayers != null && _config.ParallaxLayers.Length > 0)
        {
            foreach (var layer in _config.ParallaxLayers)
            { DrawStarLayer(handle, layer.Tile, layer.Slowness, layer.StarsPerTile, layer.Color, layer.Seed); } return;
        }
        DrawStarLayer(handle, tile: 256f, slowness: 0.30f, starsPerTile: 8, color: new Color(255, 255, 255, 20), seed: 13);
        DrawStarLayer(handle, tile: 512f, slowness: 0.60f, starsPerTile: 4, color: new Color(200, 220, 255, 35), seed: 37);
    }

    private void DrawStarLayer(DrawingHandleScreen handle, float tile, float slowness, int starsPerTile, Color color, int seed)
    {
        var center = CalculateOffsetPx();
        var parallaxOffset = -_offsetWorld * Ppd * slowness;
        var origin = parallaxOffset + center;
        var startX = (float) Math.Floor((-origin.X) / tile) * tile;
        var startY = (float) Math.Floor((-origin.Y) / tile) * tile;
        for (var x = startX; x < PixelSize.X - startX + tile; x += tile)
        {
            for (var y = startY; y < PixelSize.Y - startY + tile; y += tile)
            {
                var tx = (int) Math.Floor((x + origin.X) / tile);
                var ty = (int) Math.Floor((y + origin.Y) / tile);
                for (var s = 0; s < starsPerTile; s++)
                {
                    var rx = Hash01(tx + s * 17, ty + s * 31, seed);
                    var ry = Hash01(tx + s * 47, ty + s * 97, seed);
                    var px = (tx * tile - origin.X) + rx * tile + center.X;
                    var py = (ty * tile - origin.Y) + ry * tile + center.Y;
                    var pos = new Vector2(px, py);
                    if (pos.X < -4 || pos.Y < -4 || pos.X > PixelSize.X + 4 || pos.Y > PixelSize.Y + 4) continue;
                    handle.DrawRect(new UIBox2(pos - new Vector2(1, 1), pos + new Vector2(1, 1)), color);
                }
            }
        }
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        var localMouse = GetMouseLocalPx();
        var center = CalculateOffsetPx();
        var worldBefore = _offsetWorld + (localMouse - center) / Ppd;
        var delta = args.Delta.Y;
        var newZoom = Zoom * (1f + 0.1f * delta);
        SetZoom(newZoom);
        var worldAfterOffset = worldBefore - (localMouse - center) / Ppd;
        _offsetWorld = worldAfterOffset;
        base.MouseWheel(args);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        if (args.Function == EngineKeyFunctions.UIClick)
        {
            _isDragging = true;
            _dragAccumulated = 0f;
            _lastMouseLocal = GetMouseLocalPx();
        }
        base.KeyBindDown(args);
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        if (_isDragging)
        {
            var cur = GetMouseLocalPx();
            var delta = cur - _lastMouseLocal;
            _lastMouseLocal = cur;
            _dragAccumulated += delta.Length();
            _offsetWorld -= delta / Ppd;
            UpdateDraw();
        }
        base.MouseMove(args);
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        if (args.Function == EngineKeyFunctions.UIClick)
        {
            var wasDragging = _dragAccumulated > 3f;
            _isDragging = false;
            if (!wasDragging && _hoveredStar.HasValue)
            {
                var star = _hoveredStar.Value;
                OnStarSelect?.Invoke(star);
            }
        }
        base.KeyBindUp(args);
    }

    public bool IsAdjacentToCurrent(Star star)
    {
        if (star.Position == Vector2.Zero) return false;
        return _adjacentTargetMaps.Contains(star.Map);
    }

    private bool IsSectorStar(MapId mapId)
    { return _sectorIdByMap.ContainsKey(mapId); }

    private Color GetSectorColor(MapId mapId)
    {
        if (_sectorColorOverrideHexByMap.TryGetValue(mapId, out var hex))
        { try { return Color.FromHex(hex); } catch { } }
        if (!_sectorIdByMap.TryGetValue(mapId, out var sid)) return Color.White;
        if (sid == "FrontierSector" || sid == "CentCom")
        {
            try
            {
                if (_config != null)
                {
                    foreach (var sp in _config.SpecialSectors)
                    { if (string.Equals(sp.Id, sid, StringComparison.Ordinal) && sp.Color != null) return sp.Color.Value; }
                }
            }
            catch { }
            return Color.White;
        }
        try
        {
            if (_proto.TryIndex<SectorSystemPrototype>(sid, out var proto) && proto.StarmapColor != null) return proto.StarmapColor.Value;
        }
        catch { }
        return Color.White;
    }

    private float GetSectorSize(MapId mapId)
    { return _sectorIdByMap.ContainsKey(mapId) ? 7f : 7f; }
}
