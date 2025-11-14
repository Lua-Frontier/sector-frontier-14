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
    private readonly HashSet<MapId> _capturingMaps = new();
    private float _accum;

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
        UpdateCapturingState();
        if (_accum >= 60f)
        {
            _accum = 0f;
            RecomputeOwnership();
        }
    }

    public IReadOnlyDictionary<MapId, string> GetOwnerByMap() => _ownerByMap;

    public IReadOnlyDictionary<MapId, string> GetSectorColorOverridesHex() => _sectorColorOverrideHex;

    public IReadOnlyCollection<MapId> GetCapturingMaps() => _capturingMaps;

    private void UpdateCapturingState()
    {
        var changed = false;
        var newCapturing = new HashSet<MapId>();
        var capturingColorByMap = new Dictionary<MapId, string>();
        var captureQuery = AllEntityQuery<SectorCaptureComponent, TransformComponent>();
        while (captureQuery.MoveNext(out var uid, out var comp, out var xform))
        {
            if (!comp.IsCapturing || xform.MapID == MapId.Nullspace)
                continue;
            if (!IsOnBeaconGrid(xform.MapID, xform))
                continue;
            newCapturing.Add(xform.MapID);
            if (!string.IsNullOrWhiteSpace(comp.ColorHex)) capturingColorByMap[xform.MapID] = comp.ColorHex;
        }
        var started = new HashSet<MapId>(newCapturing);
        started.ExceptWith(_capturingMaps);
        foreach (var map in started)
        {
            if (capturingColorByMap.TryGetValue(map, out var capHex) && !string.IsNullOrWhiteSpace(capHex))
            {
                _sectorColorOverrideHex[map] = capHex;
                changed = true;
            }
            else if (TryGetOwnerAndColor(map, out var _, out var colorHex) && !string.IsNullOrWhiteSpace(colorHex))
            {
                _sectorColorOverrideHex[map] = colorHex!;
                changed = true;
            }
        }
        var ended = new HashSet<MapId>(_capturingMaps);
        ended.ExceptWith(newCapturing);
        foreach (var map in ended)
        {
            if (TryGetOwnerAndColor(map, out var _, out var colorHex) && !string.IsNullOrWhiteSpace(colorHex))
            { _sectorColorOverrideHex[map] = colorHex!; changed = true; }
        }
        if (started.Count > 0 || ended.Count > 0 || changed)
        {
            _capturingMaps.Clear();
            foreach (var m in newCapturing) _capturingMaps.Add(m);
            TryRefreshConsoles();
        }
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
        var capturingMaps = new HashSet<MapId>(_capturingMaps);

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
        try { EntityManager.System<ShuttleConsoleSystem>().RefreshStarMapForOpenConsoles(); }
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


