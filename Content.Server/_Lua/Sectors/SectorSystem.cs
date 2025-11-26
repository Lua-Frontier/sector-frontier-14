// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Server._NF.GameRule;
using Content.Server._NF.GameTicking.Events;
using Content.Server._NF.Station.Systems;
using Content.Server._NF.SectorServices;
using Content.Server._NF.Smuggling.Components;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Maps;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._Lua.Sectors;
using Content.Shared.GameTicking;
using Content.Shared.Lua.CLVar;
using Content.Shared.Parallax;
using Content.Shared.Shuttles.Components;
using Content.Shared.Whitelist;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using System.Linq;
using System.Numerics;

namespace Content.Server._Lua.Sectors;

public sealed class SectorSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly IPrototypeManager _protos = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly IRobustRandom _rng = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly MapLoaderSystem _loader = default!;
    [Dependency] private readonly StationRenameWarpsSystems _renameWarps = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;


    private sealed class SectorInstance
    {
        public SectorSystemPrototype Config = default!;
        public MapId MapId;
        public EntityUid MapUid;
        public EntityUid StationGrid;
        public readonly List<Vector2> OccupiedPoiCoords = new();
    }

    private readonly Dictionary<string, SectorInstance> _instances = new();


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStart);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
        SubscribeLocalEvent<SectorComponent, ComponentStartup>(OnGenericSectorStartup);
    }

    private void OnGenericSectorStartup(Entity<SectorComponent> ent, ref ComponentStartup args)
    {
        if (!ent.Comp.Enabled) return;
        if (!_cfg.GetCVar(CLVars.AsteroidSectorEnabled)) return;
        if (ent.Comp.Configs != null && ent.Comp.Configs.Count > 0)
        {
            foreach (var cfg in ent.Comp.Configs)
            { if (!string.IsNullOrWhiteSpace(cfg)) EnsureSector(cfg); }
            return;
        }
        if (!string.IsNullOrWhiteSpace(ent.Comp.Config)) EnsureSector(ent.Comp.Config);
    }

    private void OnRoundStart(RoundStartingEvent ev)
    {
        foreach (var proto in _protos.EnumeratePrototypes<SectorSystemPrototype>())
        {
            if (!proto.AutoStart) continue;
            if (!_cfg.GetCVar(CLVars.AsteroidSectorEnabled)) continue;
            var currentPreset = _ticker.CurrentPreset?.ID;
            if (!string.IsNullOrEmpty(proto.RequiredGamePreset) || (proto.RequiredGamePresets != null && proto.RequiredGamePresets.Length > 0))
            {
                if (string.IsNullOrEmpty(currentPreset)) continue;
                var allowed = false;
                if (!string.IsNullOrEmpty(proto.RequiredGamePreset) && string.Equals(proto.RequiredGamePreset, currentPreset, StringComparison.Ordinal))
                { allowed = true; }
                if (!allowed && proto.RequiredGamePresets != null && proto.RequiredGamePresets.Contains(currentPreset))
                { allowed = true; }
                if (!allowed) continue;
            }
            EnsureSector(proto.ID);
        }

        foreach (var inst in _instances.Values)
        {
            if (inst.Config.WorldgenConfig == null) continue;
            if (!_protos.TryIndex<Content.Server.Worldgen.Prototypes.WorldgenConfigPrototype>(inst.Config.WorldgenConfig, out var cfg)) continue;
            var ser = IoCManager.Resolve<Robust.Shared.Serialization.Manager.ISerializationManager>();
            cfg.Apply(inst.MapUid, ser, EntityManager);
        }
    }

    private void OnCleanup(RoundRestartCleanupEvent ev)
    {
        foreach (var kv in _instances.Values)
        {
            if (kv.StationGrid.IsValid()) QueueDel(kv.StationGrid);
            if (_map.MapExists(kv.MapId)) _map.DeleteMap(kv.MapId);
        }
        _instances.Clear();
    }

    public bool TryGetMapId(string configId, out MapId mapId)
    {
        mapId = MapId.Nullspace;
        if (_instances.TryGetValue(configId, out var inst))
        { mapId = inst.MapId; return true; }
        return false;
    }

    public void EnsureSector(string configId)
    {
        if (_instances.ContainsKey(configId)) return;
        if (!_protos.TryIndex<SectorSystemPrototype>(configId, out var cfg)) { Log.Error($"Sector config '{configId}' not ffund"); return; }
        Log.Info($"[SectorSystem] EnsureSector begin id='{configId}' name='{cfg.Name}' station='{cfg.Station}'");
        var preset = _ticker.CurrentPreset?.ID;
        if (!string.IsNullOrEmpty(cfg.RequiredGamePreset) || (cfg.RequiredGamePresets != null && cfg.RequiredGamePresets.Length > 0))
        {
            if (string.IsNullOrEmpty(preset)) return;
            var allowed = false;
            if (!string.IsNullOrEmpty(cfg.RequiredGamePreset) && string.Equals(cfg.RequiredGamePreset, preset, StringComparison.Ordinal))
            { allowed = true; }
            if (!allowed && cfg.RequiredGamePresets != null && cfg.RequiredGamePresets.Contains(preset))
            { allowed = true; }
            if (!allowed) return;
        }
        var mapUid = _map.CreateMap(out var mapId, false);
        var opts = Robust.Shared.EntitySerialization.DeserializationOptions.Default with { InitializeMaps = true };
        var stationGrid = _ticker.MergeGameMap(_protos.Index<GameMapPrototype>(cfg.Station), mapId, opts).FirstOrNull(HasComp<BecomesStationComponent>)!.Value;
        _meta.SetEntityName(mapUid, cfg.Name);
        EnsureComp<SectorAtmosSupportComponent>(mapUid);
        if (stationGrid.IsValid())
        {
            EnsureComp<StationSectorServiceHostComponent>(stationGrid);
            if (cfg.DeadDropEnabled)
            {
                EnsureComp<StationDeadDropComponent>(stationGrid);
                var deadDropComp = Comp<StationDeadDropComponent>(stationGrid);
                deadDropComp.MaxDeadDrops = cfg.DeadDropCount;
            }
        }
        if (cfg.ParallaxPool.Length > 0)
        {
            var parallax = EnsureComp<ParallaxComponent>(mapUid);
            parallax.Parallax = _rng.Pick(cfg.ParallaxPool);
        }
        _instances[configId] = new SectorInstance
        {
            Config = cfg,
            MapId = mapId,
            MapUid = mapUid,
            StationGrid = stationGrid
        };
        Log.Info($"[SectorSystem] Generating POIs for '{configId}'... groups={cfg.POIGroups.Length} [{string.Join(',', cfg.POIGroups.Select(g => g.Group))}]");
        GeneratePOIs(mapId, mapUid, cfg, out _);
        if (cfg.AddFtlDestination)
        { if (_shuttle.TryAddFTLDestination(mapId, true, false, false, out var ftl)) { ApplyFtlWhitelist((ftl.Owner, ftl), cfg.FtlWhitelist); } }
        _map.InitializeMap(mapUid); Log.Info($"[SectorSystem] EnsureSector done id='{configId}' map='{mapId}'");
        RaiseLocalEvent(new StationsGeneratedEvent());
    }

    private void ApplyFtlWhitelist(Entity<FTLDestinationComponent?> ent, string[]? components)
    {
        if (components == null || components.Length == 0)
        { _shuttle.SetFTLWhitelist(ent, null); return; }
        var whitelist = new EntityWhitelist
        {
            RequireAll = false,
            Components = components
        }; _shuttle.SetFTLWhitelist(ent, whitelist);
    }

    private void GeneratePOIs(MapId mapId, EntityUid mapUid, SectorSystemPrototype cfg, out List<EntityUid> spawnedPOIs)
    {
        spawnedPOIs = new List<EntityUid>();
        var preset = _ticker.CurrentPreset?.ID ?? string.Empty;
        var defaultPreset = cfg.DefaultGamePreset ?? string.Empty;
        var inst = _instances.Values.FirstOrDefault(i => i.MapId == mapId);
        if (inst == null) return;
        foreach (var group in cfg.POIGroups)
        {
            var candidates = new List<PointOfInterestPrototype>();
            foreach (var location in _protos.EnumeratePrototypes<PointOfInterestPrototype>())
            {
                if (location.SpawnGroup != group.Group) continue;
                if (location.SpawnGamePreset.Length > 0)
                {
                    var ok = false;
                    if (preset.Length > 0 && location.SpawnGamePreset.Contains(preset)) ok = true;
                    else if (defaultPreset.Length > 0 && location.SpawnGamePreset.Contains(defaultPreset)) ok = true;
                    if (!ok) continue;
                }
                candidates.Add(location);
            }
            if (candidates.Count == 0)
            { Log.Warning($"[SectorSystem] No POI candidates for group '{group.Group}' preset='{preset}'"); continue; }
            if (group.Count <= 0)
            {
                foreach (var proto in candidates)
                {
                    var offset = GetRandomCoord(inst, proto.MinimumDistance, proto.MaximumDistance);
                    if (TrySpawnPoiGrid(mapId, proto, offset, out var gridUid) && gridUid.HasValue) spawnedPOIs.Add(gridUid.Value);
                }
                continue;
            }
            if (group.Ring)
            {
                var rotation = 2 * Math.PI / group.Count;
                var rotationOffset = _rng.NextAngle() / group.Count;
                for (int i = 0; i < group.Count; i++)
                {
                    var proto = _rng.Pick(candidates);
                    Vector2i offset = new Vector2i(_rng.Next(proto.MinimumDistance, proto.MaximumDistance), 0);
                    offset = offset.Rotate(rotationOffset);
                    rotationOffset += rotation;
                    string overrideName = proto.Name + (i < 26 ? $" {(char)('A' + i)}" : $" {i + 1}");
                    if (TrySpawnPoiGrid(mapId, proto, offset, out var gridUid, overrideName) && gridUid.HasValue)
                    { spawnedPOIs.Add(gridUid.Value); Log.Info($"[SectorSystem] Spawned POI '{proto.ID}' as '{overrideName}' at {offset}"); }
                }
            }
            else
            {
                _rng.Shuffle(candidates);
                int spawned = 0;
                foreach (var proto in candidates)
                {
                    if (spawned >= group.Count) break;
                    var offset = GetRandomCoord(inst, proto.MinimumDistance, proto.MaximumDistance);
                    if (TrySpawnPoiGrid(mapId, proto, offset, out var gridUid) && gridUid.HasValue)
                    { spawnedPOIs.Add(gridUid.Value); Log.Info($"[SectorSystem] Cgfdy POI '{proto.ID}' at {offset}"); spawned++; }
                }
            }
        }
    }

    private bool TrySpawnPoiGrid(MapId mapUid, PointOfInterestPrototype proto, Vector2 offset, out EntityUid? gridUid, string? overrideName = null)
    {
        gridUid = null;
        if (!_loader.TryLoadGrid(mapUid, proto.GridPath, out var loadedGrid, offset: offset, rot: _rng.NextAngle()))
        { Log.Warning($"[SectorSystem] Failed to load POI grid '{proto.GridPath}' for '{proto.ID}'"); return false; }
        gridUid = loadedGrid.Value;
        List<EntityUid> gridList = new() { loadedGrid.Value };
        string stationName = string.IsNullOrEmpty(overrideName) ? proto.Name : overrideName;
        EntityUid? stationUid = null;
        if (_protos.TryIndex<GameMapPrototype>(proto.ID, out var stationProto)) stationUid = _station.InitializeNewStation(stationProto.Stations[proto.ID], gridList, stationName);
        var meta = EnsureComp<MetaDataComponent>(loadedGrid.Value);
        _meta.SetEntityName(loadedGrid.Value, stationName, meta);
        EntityManager.AddComponents(loadedGrid.Value, proto.AddComponents);
        if (proto.NameWarp)
        {
            bool? hideWarp = proto.HideWarp ? true : null;
            if (stationUid != null) _renameWarps.SyncWarpPointsToStation(stationUid.Value, forceAdminOnly: hideWarp);
            else _renameWarps.SyncWarpPointsToGrids(gridList, forceAdminOnly: hideWarp);
        }
        return true;
    }

    private Vector2 GetRandomCoord(SectorInstance inst, float minRange, float maxRange)
    {
        Vector2 coords = _rng.NextVector2(minRange, maxRange);
        for (int i = 0; i < 8; i++)
        {
            bool valid = true;
            foreach (var taken in inst.OccupiedPoiCoords)
            {
                if (Vector2.Distance(taken, coords) < minRange * 0.5f)
                {
                    valid = false;
                    break;
                }
            }
            if (valid) break;
            coords = _rng.NextVector2(minRange, maxRange);
        }
        inst.OccupiedPoiCoords.Add(coords);
        return coords;
    }
}


