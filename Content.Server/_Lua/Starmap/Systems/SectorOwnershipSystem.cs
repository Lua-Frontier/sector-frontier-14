// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Server.Station.Components;
using Robust.Shared.Map;
using Content.Server.Shuttles.Systems;
using Content.Server._Lua.Starmap.Components;
using System.Linq;
using System.Numerics;

namespace Content.Server._Lua.Starmap.Systems;

public sealed class SectorOwnershipSystem : EntitySystem
{
    private readonly Dictionary<MapId, string> _ownerByMap = new();
    private readonly Dictionary<MapId, string> _sectorColorOverrideHex = new();
    private float _accum;
    private float _blinkAccumulator = 0f;

    public override void Initialize()
    {
        base.Initialize();
        _accum = 0f;
        SubscribeLocalEvent<StarMapSectorColorOverrideComponent, ComponentStartup>(OnColorOverrideAdded);
        SubscribeLocalEvent<StarMapSectorColorOverrideComponent, ComponentShutdown>(OnColorOverrideRemoved);
        SubscribeLocalEvent<StarMapSectorColorOverrideComponent, AnchorStateChangedEvent>(OnBannerAnchorChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _accum += frameTime;
        _blinkAccumulator += frameTime;
        if (_blinkAccumulator > 10f) _blinkAccumulator = 0f;
        UpdateCapturingColors();
        if (_accum >= 60f)
        {
            _accum = 0f;
            RecomputeOwnership();
        }
    }

    public IReadOnlyDictionary<MapId, string> GetOwnerByMap() => _ownerByMap;

    public IReadOnlyDictionary<MapId, string> GetSectorColorOverridesHex() => _sectorColorOverrideHex;

    private void UpdateCapturingColors() // srry C# gods, it's my shitcode....
    {
        var colorChanged = false;
        var captureQuery = AllEntityQuery<SectorCaptureComponent, TransformComponent>();
        while (captureQuery.MoveNext(out var uid, out var comp, out var xform))
        {
            if (!comp.IsCapturing || xform.MapID == MapId.Nullspace) continue;
            if (!IsOnBeaconGrid(xform.MapID, xform)) continue;
            var mapId = xform.MapID;
            var blinkCycle = _blinkAccumulator % 1.0f;
            var t = MathF.Abs(MathF.Sin(blinkCycle * MathF.PI));
            Color factionColor;
            try { factionColor = Color.FromHex(comp.ColorHex); }
            catch { factionColor = Color.White; }
            var neutralColor = Color.White;
            var blinkColor = Color.InterpolateBetween(neutralColor, factionColor, t);
            var blinkHex = blinkColor.ToHex();
            if (!_sectorColorOverrideHex.TryGetValue(mapId, out var currentColor) || currentColor != blinkHex)
            {
                _sectorColorOverrideHex[mapId] = blinkHex;
                colorChanged = true;
            }
        }
        if (colorChanged)
        { TryRefreshConsoles(); }
    }

    private void OnColorOverrideAdded(Entity<StarMapSectorColorOverrideComponent> ent, ref ComponentStartup args)
    {
        try
        {
            var mapId = Transform(ent).MapID;
            if (mapId != MapId.Nullspace) { RecomputeOwnership(); TryRefreshConsoles(); }
        }
        catch { }
    }

    private void OnColorOverrideRemoved(Entity<StarMapSectorColorOverrideComponent> ent, ref ComponentShutdown args)
    {
        try
        {
            var mapId = Transform(ent).MapID;
            if (mapId != MapId.Nullspace) { RecomputeOwnership(); TryRefreshConsoles(); }
        }
        catch { }
    }

    private void OnBannerAnchorChanged(Entity<StarMapSectorColorOverrideComponent> ent, ref AnchorStateChangedEvent args)
    {
        RecomputeOwnership();
        TryRefreshConsoles();
    }

    private void RecomputeOwnership()
    {
        var newOwners = new Dictionary<MapId, string>();
        var newColors = new Dictionary<MapId, string>();
        var capturingMaps = new HashSet<MapId>();
        var captureQuery = AllEntityQuery<SectorCaptureComponent, TransformComponent>();
        while (captureQuery.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.IsCapturing && xform.MapID != MapId.Nullspace)
            { capturingMaps.Add(xform.MapID); }
        }

        var starMapQuery = AllEntityQuery<Content.Shared._Lua.Starmap.Components.StarMapComponent>();
        while (starMapQuery.MoveNext(out var uid, out var starMap))
        {
            foreach (var star in starMap.StarMap)
            {
                if (star.GlobalPosition == Vector2.Zero) continue;
                var mapId = star.Map;
                if (TryGetOwnerAndColor(mapId, out var owner, out var color))
                {
                    newOwners[mapId] = owner;
                    if (!string.IsNullOrWhiteSpace(color)) newColors[mapId] = color!;
                }
            }
        }
        var changed = false;
        var keySnapshot = new List<MapId>(_ownerByMap.Keys);
        foreach (var key in keySnapshot)
        { if (!newOwners.TryGetValue(key, out var newVal)) { _ownerByMap.Remove(key); changed = true; } }
        foreach (var (k, v) in newOwners)
        { if (!_ownerByMap.TryGetValue(k, out var old) || old != v) { _ownerByMap[k] = v; changed = true; } }
        foreach (var key in _sectorColorOverrideHex.Keys.ToList())
        {
            if (!newColors.TryGetValue(key, out var _) && !capturingMaps.Contains(key))
            {
                _sectorColorOverrideHex.Remove(key);
                changed = true;
            }
        }

        foreach (var (k, v) in newColors)
        {
            if (!capturingMaps.Contains(k))
            {
                if (!_sectorColorOverrideHex.TryGetValue(k, out var old) || old != v)
                {
                    _sectorColorOverrideHex[k] = v;
                    changed = true;
                }
            }
        }
        if (changed) TryRefreshConsoles();
    }

    private bool TryGetOwnerAndColor(MapId mapId, out string owner, out string? colorHex)
    {
        owner = string.Empty;
        colorHex = null;
        try
        {
            var beaconGrid = FindBeaconGrid(mapId);
            if (beaconGrid == null) return false;
            var query = AllEntityQuery<StarMapSectorColorOverrideComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var banner, out var xform))
            {
                if (xform.GridUid != beaconGrid || !xform.Anchored) continue;
                if (!string.IsNullOrWhiteSpace(banner.Faction))
                {
                    owner = banner.Faction;
                    colorHex = banner.ColorHex;
                    return true;
                }
            }
        }
        catch { }
        return false;
    }

    private EntityUid? FindBeaconGrid(MapId mapId)
    {
        try
        {
            var query = AllEntityQuery<BecomesStationComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var becomes, out var xform))
            {
                if (xform.MapID != mapId) continue;
                if (string.Equals(becomes.Id, "Beacon", StringComparison.Ordinal)) return uid;
            }
        }
        catch { }
        return null;
    }

    public bool IsOnBeaconGrid(MapId mapId, TransformComponent xform)
    {
        try
        {
            var beaconGrid = FindBeaconGrid(mapId);
            if (beaconGrid == null) return false;
            return xform.GridUid == beaconGrid;
        }
        catch { return false; }
    }

    private void TryRefreshConsoles()
    {
        try { EntityManager.System<ShuttleConsoleSystem>().RefreshShuttleConsoles(); }
        catch { }
    }
}

[RegisterComponent]
public sealed partial class StarMapSectorColorOverrideComponent : Component
{
    [DataField]
    public string ColorHex = "#96B089";

    [DataField]
    public string Faction = string.Empty;
}


