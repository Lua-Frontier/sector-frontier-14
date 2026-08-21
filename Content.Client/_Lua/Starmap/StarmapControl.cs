// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Client._Lua.Styles;
using Content.Shared._Lua.Starmap;
using Content.Shared._Lua.Starmap.Components;
using Content.Shared._Mono.Company;
using Content.Shared.Lua.CLVar;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.Client._Lua.Starmap;

public sealed class StarmapControl : Control
{
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEntityManager _ent = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IResourceCache _res = default!;
    public float Range = 1f;
    public float Zoom { get; private set; } = 1f;
    private List<Star> _stars = new List<Star>();
    private float _basePpd = 90f;
    public event Action<Star>? OnStarSelect;
    public event Action<string>? OnZoneSelect;
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
    private readonly Dictionary<MapId, Color> _overrideColorCache = new();
    private readonly Dictionary<MapId, Color> _sectorColorCache = new();
    private HashSet<MapId> _capturingMaps = new();
    private StarmapConfigPrototype? _config;
    private int _centerStarIndex = -1;
    private bool _graphDirty;
    private readonly Dictionary<string, Texture> _factionIconCache = new();
    private const float FactionBadgeBgAlpha = 0.63f;
    private const float LodDecorFadeStart = 0.90f;
    private const float LodDecorFadeEnd = 0.48f;
    private const float LodStarFadeStart = 0.52f;
    private const float LodStarFadeEnd = 0.22f;
    private const float LodZoneLabelFadeStart = 0.30f;
    private const float LodZoneLabelFadeEnd = 0.12f;
    private const float LodVisibleEpsilon = 0.02f;

    private bool _sectorsGloballyUnlocked;
    private Vector2[] _zoneBorderScratch = Array.Empty<Vector2>();
    private Vector2[] _zoneTriScratch = Array.Empty<Vector2>();
    private int[] _zoneEarIndices = Array.Empty<int>();
    private bool _earPreferPositiveCross;
    private readonly List<(string ZoneId, UIBox2 Box)> _zoneBadgeHits = new();
    private string? _selectedZoneId;

    public StarmapControl()
    {
        IoCManager.InjectDependencies(this);
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
    {
        _stars = stars;
        InvalidateGraph();
    }

    public void SetEdges(List<HyperlaneEdge> edges)
    {
        _edges = edges;
        InvalidateGraph();
    }

    public void SetVisibleSectorMaps(List<MapId> maps)
    {
        _visibleSectorMaps = new HashSet<MapId>(maps ?? new List<MapId>());
        InvalidateGraph();
    }

    public void SetSectorsGloballyUnlocked(bool unlocked)
    {
        _sectorsGloballyUnlocked = unlocked;
        InvalidateGraph();
    }

    public void SetSectorIdByMap(Dictionary<MapId, string> map)
    {
        _sectorIdByMap = map ?? new Dictionary<MapId, string>();
        InvalidateGraph();
    }

    public void SetOwnerByMap(Dictionary<MapId, string> owners)
    { _ownerByMap = owners ?? new Dictionary<MapId, string>(); }

    public void SetSectorColorOverridesHex(Dictionary<MapId, string> overrides)
    {
        _sectorColorOverrideHexByMap = overrides ?? new Dictionary<MapId, string>();
        RebuildOverrideCache();
        RebuildSectorColorCache();
    }

    public void SetCapturingMaps(HashSet<MapId> capturing)
    { _capturingMaps = capturing ?? new HashSet<MapId>(); }

    public bool IsCapturing(MapId mapId) => _capturingMaps != null && _capturingMaps.Contains(mapId);

    private void InvalidateGraph()
    { _graphDirty = true; }

    private void EnsureGraphUpToDate()
    {
        if (!_graphDirty) return;
        RebuildAdjacency();
        _graphDirty = false;
    }

    private void RebuildAdjacency()
    {
        _adjacentTargetMaps.Clear();
        _centerStarIndex = -1;
        if (_stars == null || _stars.Count == 0) return;
        var centerIndex = 0;
        var minDistance = float.MaxValue;
        for (var i = 0; i < _stars.Count; i++)
        {
            var distance = Vector2.Distance(_stars[i].Position, Vector2.Zero);
            if (distance < minDistance)
            { minDistance = distance; centerIndex = i; }
        }
        _centerStarIndex = centerIndex;
        if (_edges == null || _edges.Count == 0) return;
        foreach (var e in _edges)
        {
            if (e.A < 0 || e.B < 0 || e.A >= _stars.Count || e.B >= _stars.Count) continue;
            if (!IsStarVisible(_stars[e.A]) || !IsStarVisible(_stars[e.B])) continue;
            if (e.A == _centerStarIndex) _adjacentTargetMaps.Add(_stars[e.B].Map);
            if (e.B == _centerStarIndex) _adjacentTargetMaps.Add(_stars[e.A].Map);
        }
    }

    private void RebuildOverrideCache()
    {
        _overrideColorCache.Clear();
        if (_sectorColorOverrideHexByMap == null) return;
        foreach (var (mapId, hex) in _sectorColorOverrideHexByMap)
        {
            if (string.IsNullOrWhiteSpace(hex)) continue;
            try { _overrideColorCache[mapId] = Color.FromHex(hex); }
            catch { }
        }
    }

    private void RebuildSectorColorCache()
    {
        _sectorColorCache.Clear();
        if (_sectorIdByMap == null) return;

        Dictionary<string, Color>? dataColors = null;
        try
        {
            if (StarmapDataComposer.TryCompose(_proto, "StarmapData", out var data))
            {
                dataColors = new Dictionary<string, Color>();
                foreach (var def in data.Stars)
                {
                    if (def.Color != null)
                        dataColors[def.Id] = def.Color.Value;
                }
            }
        }
        catch { }

        foreach (var (mapId, sid) in _sectorIdByMap)
        {
            if (_overrideColorCache.TryGetValue(mapId, out var over))
            { _sectorColorCache[mapId] = over; continue; }
            if (dataColors != null && dataColors.TryGetValue(sid, out var dataColor))
            { _sectorColorCache[mapId] = dataColor; continue; }
            _sectorColorCache[mapId] = Color.White;
        }
    }

    private Color GetSectorColorCached(MapId mapId)
    {
        if (_overrideColorCache.TryGetValue(mapId, out var over)) return over;
        if (_sectorColorCache.TryGetValue(mapId, out var col)) return col;
        return Color.White;
    }

    public bool TryGetOwner(MapId mapId, out string owner)
    { return _ownerByMap.TryGetValue(mapId, out owner!); }

    public bool TryGetSectorId(MapId mapId, out string sectorId)
    { return _sectorIdByMap.TryGetValue(mapId, out sectorId!); }

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
    {
        var relative = position - _offsetWorld;
        return CalculateOffsetPx() + new Vector2(relative.X, -relative.Y) * Ppd;
    }

    private Vector2 UiDeltaToChart(Vector2 uiDelta)
    {
        return new Vector2(uiDelta.X, -uiDelta.Y) / Ppd;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        EnsureGraphUpToDate();
        base.Draw(handle);
        var bg = _config?.BackgroundColor ?? Color.FromHex("#0B0F14");
        handle.DrawRect(new UIBox2(Vector2.Zero, PixelSize), bg);
        DrawParallax(handle);
        var lines = _config?.GridLines ?? 10;
        for (var i = 0; i < lines; i++)
        {
            var xStep = PixelSize.X / lines;
            var yStep = PixelSize.Y / lines;
            var gridColor = _config?.GridColor ?? Color.FromHex("#243041");
            handle.DrawLine(new Vector2(i * xStep, 0), new Vector2(i * xStep, PixelSize.Y), gridColor.WithAlpha(0.35f));
            handle.DrawLine(new Vector2(0, i * yStep), new Vector2(PixelSize.X, i * yStep), gridColor.WithAlpha(0.35f));
        }
        DrawChartRegions(handle);
        DrawFactionZones(handle);
        DrawChartMarkers(handle);

        ComposedStarmapData? starmapData = null;
        var chartOrigin = Vector2.Zero;
        if (TryGetStarmapData(out var data))
        {
            starmapData = data;
            TryGetChartOrigin(data, out chartOrigin);
        }

        if (_stars.Count > 1 && _edges != null && _edges.Count > 0)
        {
            foreach (var e in _edges)
            {
                if (e.A < 0 || e.B < 0 || e.A >= _stars.Count || e.B >= _stars.Count) continue;
                if (!IsStarVisible(_stars[e.A]) || !IsStarVisible(_stars[e.B])) continue;
                var edgeLod = MathF.Min(
                    GetStarLodAlpha(_stars[e.A], starmapData),
                    GetStarLodAlpha(_stars[e.B], starmapData));
                if (edgeLod <= LodVisibleEpsilon)
                    continue;
                var fromPos = GetPositionOfStar(_stars[e.A].Position);
                var toPos = GetPositionOfStar(_stars[e.B].Position);
                LunaDraw.Line(handle, fromPos, toPos, LunaWindowStyle.TextMuted.WithAlpha(0.45f * edgeLod));
            }
        }
        _hoveredStar = null;
        var localMouse = GetMouseLocalPx();

        foreach (var star in _stars)
        {
            if (!IsStarVisible(star)) continue;
            var lodAlpha = GetStarLodAlpha(star, starmapData);
            if (lodAlpha <= LodVisibleEpsilon)
                continue;

            var uiPosition = GetPositionOfStar(star.Position);
            var isSector = IsSectorStar(star.Map);
            var isDecorative = !star.CanWarp && !isSector;
            var radius = isDecorative ? 2.5f : 5f;
            var hovered = lodAlpha > 0.15f
                && Vector2.Distance(localMouse, uiPosition) <= MathF.Max(radius * 1.8f, 6f);
            var color = Color.White;
            var name = FormatStarName(star.Name);
            var capturing = _capturingMaps != null && _capturingMaps.Contains(star.Map);
            if (capturing)
            {
                var factionCol = GetSectorColorCached(star.Map);
                color = factionCol;
            }
            if (!capturing && Vector2.Distance(Vector2.Zero, star.Position) >= Range) color = Color.Red;
            if (!capturing && Vector2.Distance(Vector2.Zero, star.Position) >= Range * 1.5)
            {
                color = Color.DarkRed;
                name = Loc.GetString("ship-ftl-tag-oor");
            }
            if (star.Position == Vector2.Zero) color = LunaWindowStyle.Accent;
            if (isSector)
            {
                color = GetSectorColorCached(star.Map);
                radius = GetSectorSize(star.Map);
            }
            else if (_overrideColorCache.TryGetValue(star.Map, out var overrideColor))
            {
                color = overrideColor;
            }
            else if (isDecorative)
            {
                var chartPos = star.Position + chartOrigin;
                if (TryResolveDecorativeColor(starmapData, star, chartPos, out var decorativeColor))
                    color = decorativeColor;
            }

            if (hovered)
                radius = isSector ? 12f : (isDecorative ? 4f : 8f);

            Color Mul(Color c, float a) => c.WithAlpha(c.A * a * lodAlpha);

            if (isSector)
            {
                if (capturing)
                {
                    var ring = Mul(Color.White, 0.9f);
                    LunaDraw.Circle(handle, uiPosition, radius + 2f, ring, false);
                    var exPos = uiPosition + new Vector2(radius + 6f, -radius - 4f);
                    DrawLabel(handle, LunaWindowStyle.FontBody, exPos, "!", ring);
                }
                else
                {
                    var ring = Mul(color, 1f);
                    LunaDraw.Circle(handle, uiPosition, radius + 2f, ring, false);
                    LunaDraw.Circle(handle, uiPosition, radius + 1f, ring, false);
                }
                LunaDraw.Circle(handle, uiPosition, radius, Mul(color, 1f));
                LunaDraw.Circle(handle, uiPosition, radius - 2, Mul(color, 0.78f));
            }
            else if (isDecorative)
            {
                LunaDraw.Circle(handle, uiPosition, radius + 1.2f, Mul(color, 0.35f), false);
                LunaDraw.Circle(handle, uiPosition, radius, Mul(color, hovered ? 1f : 0.9f));
            }
            else
            {
                if (capturing)
                {
                    var ring = Mul(Color.White, 0.9f);
                    LunaDraw.Circle(handle, uiPosition, radius + 2f, ring, false);
                    var exPos = uiPosition + new Vector2(radius + 6f, -radius - 4f);
                    DrawLabel(handle, LunaWindowStyle.FontTiny, exPos, "!", ring);
                }
                else if (_ownerByMap != null && _ownerByMap.ContainsKey(star.Map))
                {
                    var ring = Mul(GetSectorColorCached(star.Map), 1f);
                    LunaDraw.Circle(handle, uiPosition, radius + 2f, ring, false);
                    LunaDraw.Circle(handle, uiPosition, radius + 1f, ring, false);
                }
                LunaDraw.Circle(handle, uiPosition, radius, Mul(color, 1f));
            }

            if (isSector)
            {
                var labelPos = uiPosition + new Vector2(radius + 8f, 5f);
                DrawLabel(handle, LunaWindowStyle.FontBody, labelPos, name, Mul(color, hovered ? 1f : 0.92f));
            }
            else if (isDecorative)
            {
                DrawLabel(handle, LunaWindowStyle.FontTiny, uiPosition + new Vector2(5f, -2f), name,
                    Mul(color, hovered ? 1f : 0.8f));
            }
            else
            {
                DrawLabel(handle, LunaWindowStyle.FontSmall, uiPosition + new Vector2(8f, -4f), name,
                    hovered ? Mul(LunaWindowStyle.TextPrimary, 1f) : Mul(color, 0.9f));
            }

            if (hovered)
                _hoveredStar = star;
        }
    }

    private static float LodAlpha(float zoom, float fadeStart, float fadeEnd)
    {
        if (fadeStart <= fadeEnd) return zoom >= fadeStart ? 1f : 0f;
        if (zoom >= fadeStart) return 1f;
        if (zoom <= fadeEnd) return 0f;
        var t = (zoom - fadeEnd) / (fadeStart - fadeEnd);
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private float GetStarLodAlpha(Star star, ComposedStarmapData? data)
    {
        var isSector = IsSectorStar(star.Map);
        var hasLore = StarHasDescription(star, data);
        if (isSector || hasLore) return LodAlpha(Zoom, LodStarFadeStart, LodStarFadeEnd);
        return LodAlpha(Zoom, LodDecorFadeStart, LodDecorFadeEnd);
    }

    private static bool StarHasDescription(Star star, ComposedStarmapData? data)
    {
        if (data == null)
            return false;

        var displayName = FormatStarName(star.Name);
        foreach (var def in data.Stars)
        {
            if (!string.Equals(def.Name, star.Name, StringComparison.Ordinal)
                && !string.Equals(def.Name, displayName, StringComparison.Ordinal))
                continue;

            return !string.IsNullOrWhiteSpace(def.Description)
                   || !string.IsNullOrWhiteSpace(def.DescriptionFull);
        }

        return false;
    }

    private bool TryResolveDecorativeColor(ComposedStarmapData? data, Star star, Vector2 chartPos, out Color color)
    {
        color = default;
        if (data == null)
            return false;

        foreach (var def in data.Stars)
        {
            if (!string.Equals(def.Name, star.Name, StringComparison.Ordinal)
                && !string.Equals(def.Name, FormatStarName(star.Name), StringComparison.Ordinal))
                continue;

            if (def.Color != null)
            {
                color = def.Color.Value;
                return true;
            }
            break;
        }

        foreach (var zone in data.FactionZones)
        {
            if (zone.Points == null || zone.Points.Length < 3)
                continue;
            if (!PointInPolygon(chartPos, zone.Points))
                continue;
            color = zone.Color;
            return true;
        }

        return false;
    }

    private static bool PointInPolygon(Vector2 point, Vector2[] polygon)
    {
        var inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            var pi = polygon[i];
            var pj = polygon[j];
            if (((pi.Y > point.Y) != (pj.Y > point.Y)) &&
                (point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y + float.Epsilon) + pi.X))
                inside = !inside;
        }
        return inside;
    }

    private static string FormatStarName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "???";
        const string starPrefix = "[STAR] ";
        return name.StartsWith(starPrefix, StringComparison.Ordinal)
            ? name[starPrefix.Length..]
            : name;
    }

    private void DrawLabel(DrawingHandleScreen handle, Font font, Vector2 pos, string text, Color color)
    {
        handle.DrawString(font, pos + new Vector2(1f, 1f), text, Color.Black.WithAlpha(0.55f * color.A));
        handle.DrawString(font, pos, text, color);
    }
    private static UIBox2 DrawBadgeLabel(
        DrawingHandleScreen handle,
        Font font,
        Vector2 textPos,
        string text,
        Color accent,
        float padH = 8f,
        float padV = 4f,
        float bgAlpha = FactionBadgeBgAlpha,
        Texture? icon = null,
        float iconSize = 14f,
        bool selected = false,
        float opacity = 1f)
    {
        opacity = Math.Clamp(opacity, 0f, 1f);
        const float iconGap = 5f;
        var iconBlock = icon != null ? iconSize + iconGap : 0f;
        var textSize = handle.GetDimensions(font, text, 1f);
        var contentW = iconBlock + textSize.X;
        var badgeMin = textPos - new Vector2(padH, padV);
        var badgeMax = textPos + new Vector2(contentW + padH, textSize.Y + padV);
        var box = new UIBox2(badgeMin, badgeMax);

        handle.DrawRect(box, Color.FromHex("#0B0F14").WithAlpha(bgAlpha * opacity));
        handle.DrawRect(new UIBox2(badgeMin, new Vector2(badgeMin.X + 3f, badgeMax.Y)), accent.WithAlpha(0.95f * opacity));
        handle.DrawRect(box, accent.WithAlpha((selected ? 0.95f : 0.45f) * opacity), filled: false);
        if (selected)
            handle.DrawRect(box, accent.WithAlpha(0.18f * opacity));

        var drawY = textPos.Y;
        if (icon != null)
        {
            var iconY = drawY + (textSize.Y - iconSize) * 0.5f;
            handle.DrawTextureRect(icon, UIBox2.FromDimensions(new Vector2(textPos.X, iconY), new Vector2(iconSize, iconSize)),
                Color.White.WithAlpha(0.95f * opacity));
        }

        var labelPos = textPos + new Vector2(iconBlock, 0f);
        handle.DrawString(font, labelPos + new Vector2(1f, 1f), text, Color.Black.WithAlpha(0.65f * opacity));
        handle.DrawString(font, labelPos, text, Color.White.WithAlpha(0.98f * opacity));
        return box;
    }

    private static UIBox2 DrawCenteredBadgeLabel(DrawingHandleScreen handle, Font font, Vector2 center, string text, Color accent, Texture? icon = null, float padH = 10f, float padV = 5f, bool selected = false, float opacity = 1f)
    {
        const float iconGap = 5f;
        const float iconSize = 14f;
        var iconBlock = icon != null ? iconSize + iconGap : 0f;
        var textSize = handle.GetDimensions(font, text, 1f);
        var contentW = iconBlock + textSize.X;
        var contentH = MathF.Max(textSize.Y, icon != null ? iconSize : 0f);
        var totalW = contentW + padH * 2f;
        var totalH = contentH + padV * 2f;
        var badgeMin = center - new Vector2(totalW * 0.5f, totalH * 0.5f);
        var textPos = badgeMin + new Vector2(padH, padV);
        return DrawBadgeLabel(handle, font, textPos, text, accent, padH, padV, FactionBadgeBgAlpha, icon, iconSize, selected, opacity);
    }

    private bool TryGetFactionIcon(FactionZoneDefinition zone, out Texture? texture)
    {
        texture = null;
        var cacheKey = !string.IsNullOrWhiteSpace(zone.IconPath)
            ? zone.IconPath!
            : zone.IconCompany;
        if (string.IsNullOrWhiteSpace(cacheKey))
            return false;

        if (_factionIconCache.TryGetValue(cacheKey, out var cached))
        {
            texture = cached;
            return true;
        }

        string? path = null;
        if (!string.IsNullOrWhiteSpace(zone.IconPath))
            path = zone.IconPath;
        else if (!string.IsNullOrWhiteSpace(zone.IconCompany)
                 && _proto.TryIndex<CompanyPrototype>(zone.IconCompany, out var company)
                 && !string.IsNullOrWhiteSpace(company.IconPath))
            path = company.IconPath;

        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            cached = _res.GetResource<TextureResource>(path!).Texture;
            _factionIconCache[cacheKey] = cached;
            texture = cached;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool IsStarVisible(Star star)
    {
        if (_capturingMaps != null && _capturingMaps.Contains(star.Map)) return true;
        if (star.Position == Vector2.Zero) return true;
        if (!star.CanWarp) return IsChartOnlyStarVisible(star);
        if (_visibleSectorMaps.Count > 0 || _sectorIdByMap.Count > 0)
        {
            if (!_visibleSectorMaps.Contains(star.Map))
                return false;
            return IsSectorVisibleForLocalCompany(star.Map);
        }
        return false;
    }

    private bool IsChartOnlyStarVisible(Star star)
    {
        try
        {
            var dataId = _cfg.GetCVar(CLVars.StarmapDataId);
            if (!StarmapDataComposer.TryCompose(_proto, dataId, out var data))
                return true;

            foreach (var def in data.Stars)
            {
                if (!string.Equals(def.Name, star.Name, StringComparison.Ordinal))
                    continue;

                var company = SectorVisibility.NoneCompany;
                IReadOnlyCollection<string>? learned = null;
                var local = _player.LocalEntity;
                if (local != null &&
                    _ent.TryGetComponent(local.Value, out CompanyComponent? companyComp) &&
                    !string.IsNullOrWhiteSpace(companyComp.CompanyName))
                    company = companyComp.CompanyName;
                if (local != null &&
                    _ent.TryGetComponent(local.Value, out KnownSectorsComponent? known))
                    learned = known.LearnedSectorIds;

                return SectorVisibility.IsSectorVisible(def, company, _sectorsGloballyUnlocked, learned);
            }
        }
        catch
        { }

        return true;
    }

    private bool IsSectorVisibleForLocalCompany(MapId mapId)
    {
        if (!_sectorIdByMap.TryGetValue(mapId, out var sectorId) || string.IsNullOrWhiteSpace(sectorId))
            return true;
        var company = SectorVisibility.NoneCompany;
        IReadOnlyCollection<string>? learned = null;
        var local = _player.LocalEntity;
        if (local != null &&
            _ent.TryGetComponent(local.Value, out CompanyComponent? companyComp) &&
            !string.IsNullOrWhiteSpace(companyComp.CompanyName))
            company = companyComp.CompanyName;
        if (local != null &&
            _ent.TryGetComponent(local.Value, out KnownSectorsComponent? known))
            learned = known.LearnedSectorIds;
        try
        {
            var dataId = _cfg.GetCVar(CLVars.StarmapDataId);
            if (!StarmapDataComposer.TryCompose(_proto, dataId, out var data))
                return true;
            return SectorVisibility.IsSectorVisible(data, sectorId, company, _sectorsGloballyUnlocked, learned);
        }
        catch
        {
            return true;
        }
    }

    private void DrawChartRegions(DrawingHandleScreen handle)
    {
        if (!TryGetStarmapData(out var data) || data.ChartRegions.Length == 0)
            return;

        TryGetChartOrigin(data, out var origin);
        foreach (var region in data.ChartRegions)
        {
            if (region.Points == null || region.Points.Length < 3)
                continue;

            if (_zoneBorderScratch.Length < region.Points.Length)
                _zoneBorderScratch = new Vector2[region.Points.Length];

            var centroid = Vector2.Zero;
            for (var i = 0; i < region.Points.Length; i++)
            {
                var relative = region.Points[i] - origin;
                _zoneBorderScratch[i] = GetPositionOfStar(relative);
                centroid += relative;
            }

            centroid /= region.Points.Length;
            var fillCount = TriangulatePolygon(_zoneBorderScratch, region.Points.Length, ref _zoneTriScratch);
            if (fillCount >= 3)
            {
                handle.DrawPrimitives(
                    DrawPrimitiveTopology.TriangleList,
                    new Span<Vector2>(_zoneTriScratch, 0, fillCount),
                    region.Color.WithAlpha(Math.Clamp(region.FillAlpha, 0f, 1f)));
            }

            var border = region.Color.WithAlpha(Math.Clamp(region.BorderAlpha, 0f, 1f));
            var borderCount = region.Points.Length;
            if (region.Dashed)
                LunaDraw.DashedPolyline(handle, _zoneBorderScratch, borderCount, border, region.DashLength, region.GapLength);
            else
                LunaDraw.Polyline(handle, _zoneBorderScratch, borderCount, border);

            if (region.ShowLabel && !string.IsNullOrWhiteSpace(region.Name))
            {
                var labelPos = GetPositionOfStar(centroid) + new Vector2(8f, -12f);
                DrawLabel(handle, LunaWindowStyle.FontTitle, labelPos, ResolveLocText(region.Name), border);
            }
        }
    }

    private void DrawChartMarkers(DrawingHandleScreen handle)
    {
        if (!TryGetStarmapData(out var data) || data.ChartMarkers.Length == 0)
            return;

        TryGetChartOrigin(data, out var origin);

        const float corridorDash = 18f;
        const float corridorGap = 12f;
        foreach (var marker in data.ChartMarkers)
        {
            if (!string.Equals(marker.Kind, "regionDivider", StringComparison.OrdinalIgnoreCase)
                || marker.EndPosition is not { } dividerEnd)
                continue;

            var from = GetPositionOfStar(marker.Position - origin);
            var to = GetPositionOfStar(dividerEnd - origin);
            LunaDraw.Line(handle, from, to, marker.Color.WithAlpha(0.85f));
        }
        foreach (var marker in data.ChartMarkers)
        {
            if (string.IsNullOrWhiteSpace(marker.LinkTo))
                continue;

            ChartMarkerDefinition? target = null;
            foreach (var other in data.ChartMarkers)
            {
                if (string.Equals(other.Id, marker.LinkTo, StringComparison.OrdinalIgnoreCase))
                {
                    target = other;
                    break;
                }
            }

            if (target == null)
                continue;

            var linkFrom = GetPositionOfStar(marker.Position - origin);
            var linkTo = GetPositionOfStar(target.Position - origin);
            var corridorColor = marker.Color.WithAlpha(0.9f);
            LunaDraw.DashedLine(handle, linkFrom, linkTo, corridorColor, corridorDash, corridorGap);

            if (!string.IsNullOrWhiteSpace(marker.LinkLabel))
            {
                var mid = (linkFrom + linkTo) * 0.5f;
                var delta = linkTo - linkFrom;
                var offset = Vector2.Zero;
                if (delta.LengthSquared() > 0.01f)
                {
                    var n = new Vector2(-delta.Y, delta.X);
                    n /= n.Length();
                    offset = n * 10f;
                }

                var label = ResolveChartMarkerText(marker.LinkLabel);
                DrawLabel(handle, LunaWindowStyle.FontTiny, mid + offset, label, corridorColor);
            }
        }

        foreach (var marker in data.ChartMarkers)
        {
            if (string.Equals(marker.Kind, "regionDivider", StringComparison.OrdinalIgnoreCase))
                continue;

            var ui = GetPositionOfStar(marker.Position - origin);
            var color = marker.Color;
            var size = MathF.Max(4f, marker.Size);

            if (string.Equals(marker.Kind, "bluespaceRift", StringComparison.OrdinalIgnoreCase))
            {
                var d = size;
                var diamond = new Vector2[]
                {
                    ui + new Vector2(0f, -d),
                    ui + new Vector2(d, 0f),
                    ui + new Vector2(0f, d),
                    ui + new Vector2(-d, 0f),
                };
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, diamond, color.WithAlpha(0.28f));
                LunaDraw.Polyline(handle, diamond, color.WithAlpha(0.95f));
                LunaDraw.Line(handle, ui + new Vector2(0f, -d * 1.35f), ui + new Vector2(0f, d * 1.35f), color.WithAlpha(0.7f));
                LunaDraw.Line(handle, ui + new Vector2(-d * 1.35f, 0f), ui + new Vector2(d * 1.35f, 0f), color.WithAlpha(0.7f));

                var ringPts = new Vector2[24];
                for (var i = 0; i < 24; i++)
                {
                    var a = (MathF.PI * 2f * i) / 24f;
                    ringPts[i] = ui + new Vector2(MathF.Cos(a), MathF.Sin(a)) * (d * 1.7f);
                }
                LunaDraw.DashedPolyline(handle, ringPts, color.WithAlpha(0.55f), 8f, 10f);
            }
            else if (string.Equals(marker.Kind, "regionLabel", StringComparison.OrdinalIgnoreCase))
            { }
            else
            {
                LunaDraw.Circle(handle, ui, size, color.WithAlpha(0.85f));
            }

            if (marker.ShowLabel && !string.IsNullOrWhiteSpace(marker.Name))
            {
                var font = string.Equals(marker.Kind, "regionLabel", StringComparison.OrdinalIgnoreCase)
                    ? LunaWindowStyle.FontTitle
                    : LunaWindowStyle.FontSmall;
                var labelOffset = string.Equals(marker.Kind, "regionLabel", StringComparison.OrdinalIgnoreCase)
                    ? new Vector2(-20f, 0f)
                    : new Vector2(size + 4f, -4f);
                var labelText = ResolveChartMarkerText(marker.Name);
                DrawLabel(handle, font, ui + labelOffset, labelText, color.WithAlpha(0.95f));
            }
        }
    }

    private static string ResolveChartMarkerText(string value) => ResolveLocText(value);

    private static string ResolveLocText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;
        if (Loc.TryGetString(value, out var localized))
            return localized;

        return value;
    }

    private void DrawFactionZones(DrawingHandleScreen handle)
    {
        if (!TryGetStarmapData(out var data) || data.FactionZones.Length == 0)
            return;

        TryGetChartOrigin(data, out var origin);
        var company = ResolveLocalCompany();
        IReadOnlyCollection<string>? learned = ResolveLocalLearned();
        _zoneBadgeHits.Clear();

        foreach (var zone in data.FactionZones)
        {
            if (zone.Points == null || zone.Points.Length < 3)
                continue;
            if (!IsFactionZoneVisible(zone, company, learned))
                continue;

            if (_zoneBorderScratch.Length < zone.Points.Length)
                _zoneBorderScratch = new Vector2[zone.Points.Length];

            var centroid = Vector2.Zero;
            for (var i = 0; i < zone.Points.Length; i++)
            {
                var relative = zone.Points[i] - origin;
                var ui = GetPositionOfStar(relative);
                _zoneBorderScratch[i] = ui;
                centroid += relative;
            }
            centroid /= zone.Points.Length;
            var fillCount = TriangulatePolygon(_zoneBorderScratch, zone.Points.Length, ref _zoneTriScratch);
            if (fillCount >= 3)
            {
                var fill = zone.Color.WithAlpha(Math.Clamp(zone.FillAlpha, 0f, 1f));
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, new Span<Vector2>(_zoneTriScratch, 0, fillCount), fill);
            }
            var border = zone.Color.WithAlpha(Math.Clamp(zone.BorderAlpha, 0f, 1f));
            LunaDraw.Polyline(handle, _zoneBorderScratch, zone.Points.Length, border);

            if (zone.ShowLabel && !string.IsNullOrWhiteSpace(zone.Name))
            {
                var zoneLabelLod = LodAlpha(Zoom, LodZoneLabelFadeStart, LodZoneLabelFadeEnd);
                if (zoneLabelLod > LodVisibleEpsilon)
                {
                    var labelCenter = GetPositionOfStar(centroid) + new Vector2(0f, -14f);
                    TryGetFactionIcon(zone, out var icon);
                    var selected = string.Equals(zone.Id, _selectedZoneId, StringComparison.OrdinalIgnoreCase);
                    var box = DrawCenteredBadgeLabel(handle, LunaWindowStyle.FontMapLabel, labelCenter, ResolveLocText(zone.Name), zone.Color, icon, selected: selected, opacity: zoneLabelLod);
                    if (zoneLabelLod > 0.2f) _zoneBadgeHits.Add((zone.Id, box));
                }
            }
        }
    }
    private int TriangulatePolygon(Vector2[] points, int count, ref Vector2[] triOut)
    {
        if (count < 3)
            return 0;

        PrepareEarWinding(points, count);

        if (_zoneEarIndices.Length < count)
            _zoneEarIndices = new int[count];

        for (var i = 0; i < count; i++)
            _zoneEarIndices[i] = i;

        var remaining = count;
        var maxTris = Math.Max(0, count - 2);
        var needed = maxTris * 3;
        if (triOut.Length < needed)
            triOut = new Vector2[needed];

        var outCount = 0;
        var guard = 0;
        var maxGuard = count * count;

        while (remaining > 3 && guard++ < maxGuard)
        {
            var earFound = false;
            for (var i = 0; i < remaining; i++)
            {
                var iPrev = (i + remaining - 1) % remaining;
                var iNext = (i + 1) % remaining;
                var a = points[_zoneEarIndices[iPrev]];
                var b = points[_zoneEarIndices[i]];
                var c = points[_zoneEarIndices[iNext]];

                var cross = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
                if (MathF.Abs(cross) <= 1e-6f)
                    continue;
                if (_earPreferPositiveCross ? cross <= 0f : cross >= 0f)
                    continue;

                var hasPointInside = false;
                for (var j = 0; j < remaining; j++)
                {
                    if (j == iPrev || j == i || j == iNext)
                        continue;
                    if (PointInTriangle(points[_zoneEarIndices[j]], a, b, c))
                    {
                        hasPointInside = true;
                        break;
                    }
                }

                if (hasPointInside)
                    continue;

                triOut[outCount++] = a;
                triOut[outCount++] = b;
                triOut[outCount++] = c;

                for (var k = i; k < remaining - 1; k++)
                    _zoneEarIndices[k] = _zoneEarIndices[k + 1];
                remaining--;
                earFound = true;
                break;
            }

            if (!earFound)
                break;
        }

        if (remaining == 3)
        {
            triOut[outCount++] = points[_zoneEarIndices[0]];
            triOut[outCount++] = points[_zoneEarIndices[1]];
            triOut[outCount++] = points[_zoneEarIndices[2]];
        }

        return outCount;
    }

    private void PrepareEarWinding(Vector2[] points, int count)
    {
        var area = 0f;
        for (var i = 0; i < count; i++)
        {
            var p = points[i];
            var q = points[(i + 1) % count];
            area += p.X * q.Y - q.X * p.Y;
        }

        _earPreferPositiveCross = area > 0f;
    }

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        var v0 = c - a;
        var v1 = b - a;
        var v2 = p - a;
        var dot00 = Vector2.Dot(v0, v0);
        var dot01 = Vector2.Dot(v0, v1);
        var dot02 = Vector2.Dot(v0, v2);
        var dot11 = Vector2.Dot(v1, v1);
        var dot12 = Vector2.Dot(v1, v2);
        var denom = dot00 * dot11 - dot01 * dot01;
        if (MathF.Abs(denom) < 1e-8f)
            return false;
        var inv = 1f / denom;
        var u = (dot11 * dot02 - dot01 * dot12) * inv;
        var v = (dot00 * dot12 - dot01 * dot02) * inv;
        return u >= 0f && v >= 0f && (u + v) <= 1f;
    }

    private bool TryGetStarmapData(out ComposedStarmapData data)
    {
        data = default!;
        try
        {
            var dataId = _cfg.GetCVar(CLVars.StarmapDataId);
            return StarmapDataComposer.TryCompose(_proto, dataId, out data!);
        }
        catch
        {
            return false;
        }
    }
    private bool TryGetChartOrigin(ComposedStarmapData data, out Vector2 origin)
    {
        origin = Vector2.Zero;
        if (_stars.Count == 0 || data.Stars.Length == 0)
            return false;

        foreach (var star in _stars)
        {
            if (string.IsNullOrWhiteSpace(star.Name))
                continue;

            foreach (var def in data.Stars)
            {
                if (!string.Equals(star.Name, def.Name, StringComparison.Ordinal)
                    && !string.Equals(star.Name, $"[STAR] {def.Name}", StringComparison.Ordinal))
                    continue;

                origin = def.Position - star.Position;
                return true;
            }
        }

        return false;
    }

    private string ResolveLocalCompany()
    {
        var local = _player.LocalEntity;
        if (local != null &&
            _ent.TryGetComponent(local.Value, out CompanyComponent? companyComp) &&
            !string.IsNullOrWhiteSpace(companyComp.CompanyName))
            return companyComp.CompanyName;
        return SectorVisibility.NoneCompany;
    }

    private IReadOnlyCollection<string>? ResolveLocalLearned()
    {
        var local = _player.LocalEntity;
        if (local != null &&
            _ent.TryGetComponent(local.Value, out KnownSectorsComponent? known))
            return known.LearnedSectorIds;
        return null;
    }

    private bool IsFactionZoneVisible(FactionZoneDefinition zone, string company, IReadOnlyCollection<string>? learned)
    {
        if (learned != null)
        {
            foreach (var id in learned)
            {
                if (string.Equals(id, zone.Id, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        if (_sectorsGloballyUnlocked)
        {
            if (!zone.ExcludeFromGlobalUnlock)
                return true;
            return IsZoneCompanyListed(zone, company) || zone.VisibleToAll;
        }

        if (zone.VisibleToAll)
            return true;

        return IsZoneCompanyListed(zone, company);
    }

    private static bool IsZoneCompanyListed(FactionZoneDefinition zone, string company)
    {
        if (zone.VisibleCompanies.Length == 0)
            return false;

        foreach (var listed in zone.VisibleCompanies)
        {
            if (string.Equals(listed, company, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
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
        var parallaxOffset = new Vector2(-_offsetWorld.X, _offsetWorld.Y) * Ppd * slowness;
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
        var worldBefore = _offsetWorld + UiDeltaToChart(localMouse - center);
        var delta = args.Delta.Y;
        var newZoom = Zoom * (1f + 0.1f * delta);
        SetZoom(newZoom);
        _offsetWorld = worldBefore - UiDeltaToChart(localMouse - center);
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
            _offsetWorld -= UiDeltaToChart(delta);
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
            if (!wasDragging)
            {
                var local = GetMouseLocalPx();
                if (TryHitZoneBadge(local, out var zoneId))
                {
                    _selectedZoneId = zoneId;
                    OnZoneSelect?.Invoke(zoneId);
                    UpdateDraw();
                }
                else if (_hoveredStar.HasValue)
                {
                    _selectedZoneId = null;
                    OnStarSelect?.Invoke(_hoveredStar.Value);
                    UpdateDraw();
                }
            }
        }
        base.KeyBindUp(args);
    }

    private bool TryHitZoneBadge(Vector2 local, out string zoneId)
    {
        zoneId = string.Empty;
        for (var i = _zoneBadgeHits.Count - 1; i >= 0; i--)
        {
            var (id, box) = _zoneBadgeHits[i];
            if (!box.Contains(local))
                continue;
            zoneId = id;
            return true;
        }

        return false;
    }

    public void ClearSelectedZone()
    {
        _selectedZoneId = null;
        UpdateDraw();
    }

    public bool IsAdjacentToCurrent(Star star)
    {
        EnsureGraphUpToDate();
        if (star.Position == Vector2.Zero) return false;
        return _adjacentTargetMaps.Contains(star.Map);
    }

    private bool IsSectorStar(MapId mapId)
    { return _sectorIdByMap.ContainsKey(mapId); }

    private float GetSectorSize(MapId mapId)
    { return _sectorIdByMap.ContainsKey(mapId) ? 7f : 7f; }
}
