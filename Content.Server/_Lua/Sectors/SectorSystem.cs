// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Server._Lua.Starmap.Systems;
using Content.Server._NF.GameRule;
using Content.Server._NF.GameTicking.Events;
using Content.Server._NF.Station.Systems;
using Content.Server._NF.SectorServices;
using Content.Server._NF.Smuggling.Components;
using Content.Server._NF.Trade;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Server._Lua.Shuttles.Systems;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._Lua.Starmap;
using Content.Shared.GameTicking;
using Content.Shared.Lua.CLVar;
using Content.Shared.Parallax;
using Content.Shared.Shuttles.Components;
using Content.Shared.Station.Components;
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

public sealed partial class SectorSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly IPrototypeManager _protos = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly IRobustRandom _rng = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly ShuttleGridAccessSystem _gridAccess = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly MapLoaderSystem _loader = default!;
    [Dependency] private readonly StationRenameWarpsSystems _renameWarps = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;

    private sealed class SectorInstance
    {
        public StarDefinition Config = default!;
        public MapId MapId;
        public EntityUid MapUid;
        public EntityUid StationGrid;
        public readonly List<Vector2> OccupiedPoiCoords = new();
    }

    private readonly Dictionary<string, SectorInstance> _instances = new();
    private Dictionary<string, StarDefinition>? _starIndex;
    private string? _hubSectorId;
    private bool _startedThisRound;
    private bool _bootstrapComplete;

    public bool CentComStarUnlocked { get; private set; }
    public float EmergencyShuttleIndex { get; set; }
    public bool BootstrapComplete => _bootstrapComplete;

    public IEnumerable<(string Id, MapId MapId, EntityUid MapUid)> EnumerateSectorMaps()
    {
        PruneDeadInstances();
        foreach (var (id, inst) in _instances)
            yield return (id, inst.MapId, inst.MapUid);
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
        SubscribeLocalEvent<SectorComponent, ComponentStartup>(OnGenericSectorStartup);
        InitializeCentComGameplay();
    }

    private void PruneDeadInstances()
    {
        List<string>? dead = null;
        foreach (var (id, inst) in _instances)
        {
            if (_map.MapExists(inst.MapId) && Exists(inst.MapUid) && !TerminatingOrDeleted(inst.MapUid))
                continue;

            dead ??= new List<string>();
            dead.Add(id);
        }

        if (dead == null)
            return;

        foreach (var id in dead)
        {
            if (!_instances.TryGetValue(id, out var inst))
                continue;

            _instances.Remove(id);
            if (_hubSectorId == id)
                _hubSectorId = null;

            _station.DeleteStationsOnMap(inst.MapId);
            RaiseLocalEvent(new SectorUnloadedEvent(id, inst.MapId));
        }
    }

    private void RebuildStarIndex()
    {
        _starIndex = new Dictionary<string, StarDefinition>();
        var dataId = _cfg.GetCVar(CLVars.StarmapDataId);
        if (!StarmapDataComposer.TryCompose(_protos, dataId, out var data))
            return;

        foreach (var star in data.Stars)
            _starIndex[star.Id] = star;
    }

    private StarDefinition? FindStar(string id)
    {
        if (_starIndex == null)
            RebuildStarIndex();
        return _starIndex != null && _starIndex.TryGetValue(id, out var star) ? star : null;
    }

    public void StartAllAutoStartSectors()
    {
        if (_startedThisRound)
            return;

        RebuildStarIndex();
        if (_starIndex == null || _starIndex.Count == 0)
            throw new SectorBootstrapException("StarmapData has no stars; cannot start round without starmap sectors");

        _startedThisRound = true;
        var preset = _ticker.CurrentPreset?.ID;
        var toStart = new List<StarDefinition>();

        foreach (var star in _starIndex.Values)
        {
            if (IsAutoStartCandidate(star, preset))
                toStart.Add(star);
        }

        try
        {
            ValidateAutoStartPlan(toStart, preset);

            toStart.Sort((a, b) =>
            {
                var oa = GetSectorStartOrder(a);
                var ob = GetSectorStartOrder(b);
                if (oa != ob)
                    return oa.CompareTo(ob);
                return string.CompareOrdinal(a.Id, b.Id);
            });

            Log.Info($"[SectorSystem] Starting {toStart.Count} autoStart sector(s) for preset '{preset ?? "<null>"}': {string.Join(", ", toStart.Select(s => s.Id))}");

            foreach (var star in toStart)
            {
                AnnounceStartup(Loc.GetString("sector-startup-begin", ("sector", GetSectorStartupName(star))));

                if (!_protos.TryIndex<GameMapPrototype>(star.Station!, out var gameMap))
                {
                    FailBootstrap(star.Station ?? star.Id, $"GameMap '{star.Station}' not found for sector '{star.Id}'");
                    return;
                }

                AnnounceStartup(Loc.GetString("sector-startup-placing", ("station", gameMap.MapName)));

                if (!TryEnsureSector(star.Id, out var error, announceProgress: true))
                {
                    FailBootstrap(gameMap.MapName, error ?? $"Failed to start sector '{star.Id}'");
                    return;
                }
            }

            if (_hubSectorId == null || !_instances.TryGetValue(_hubSectorId, out _))
            {
                FailBootstrap("Hub", "Hub sector failed to start");
                throw new SectorBootstrapException("Hub sector failed to start; ensure exactly one star has isHub: true with station + autoStart");
            }

            if (!TryGetCentCom(out _, out _, out _))
                FailBootstrap("CentComm", "CentCom sector is not available after autoStart");

            BindStationCentcomm();
            _bootstrapComplete = true;
            RaiseLocalEvent(new StationsGeneratedEvent());
        }
        catch (SectorBootstrapException)
        {
            AbortBootstrap();
            AnnounceStartup(Loc.GetString("sector-startup-abort"));
            throw;
        }
    }

    private bool IsAutoStartCandidate(StarDefinition star, string? preset)
    {
        if (string.Equals(star.StarType, "decorative", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!star.AutoStart || string.IsNullOrEmpty(star.Station))
            return false;
        if (!IsPresetAllowed(star, preset))
            return false;
        if (IsAsteroidSector(star) && !_cfg.GetCVar(CLVars.AsteroidSectorEnabled))
            return false;
        return true;
    }

    private void ValidateAutoStartPlan(List<StarDefinition> toStart, string? preset)
    {
        if (toStart.Count == 0)
            FailValidate($"нет autoStart секторов для пресета '{preset ?? "<null>"}'");

        var hubs = toStart.Where(s => s.IsHub).ToList();
        if (hubs.Count != 1)
            FailValidate($"нужен ровно один isHub среди autoStart (сейчас {hubs.Count})");

        var centComs = toStart.Where(IsCentComStar).ToList();
        if (centComs.Count != 1)
            FailValidate($"нужен ровно один CentCom среди autoStart (сейчас {centComs.Count})");

        if (!string.Equals(centComs[0].Station, "CentComm", StringComparison.OrdinalIgnoreCase))
            FailValidate($"CentCom sector '{centComs[0].Id}' must use station: CentComm");

        var duplicateStations = toStart
            .GroupBy(s => s.Station, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicateStations.Count > 0)
            FailValidate($"дублирующиеся station у autoStart: {string.Join(", ", duplicateStations)}");

        foreach (var star in toStart)
        {
            if (!_protos.TryIndex<GameMapPrototype>(star.Station!, out _))
                FailValidate($"GameMap '{star.Station}' не найден для сектора '{star.Id}'");
        }
    }

    private void FailValidate(string reason)
    {
        AnnounceStartup(Loc.GetString("sector-startup-validate-failed", ("reason", reason)));
        throw new SectorBootstrapException(reason);
    }

    private void FailBootstrap(string stationName, string error)
    {
        Log.Error($"[SectorSystem] {error}");
        AnnounceStartup(Loc.GetString("sector-startup-failed", ("station", stationName)));
        throw new SectorBootstrapException(error);
    }

    private void AnnounceStartup(string message)
    {
        Log.Info($"[SectorSystem] {message}");
        _chatManager.DispatchServerAnnouncement(message);
    }

    private static string GetSectorStartupName(StarDefinition star)
    {
        var key = $"sector-startup-name-{star.Id}";
        return Robust.Shared.Localization.Loc.TryGetString(key, out var name) ? name : star.Name;
    }

    public bool TryGetSectorDisplayName(MapId mapId, out string name)
    {
        if (!TryGetSectorConfig(mapId, out var config))
        {
            name = string.Empty;
            return false;
        }

        name = GetSectorStartupName(config);
        return true;
    }

    public string GetSectorDisplayName(MapId mapId)
    {
        if (TryGetSectorDisplayName(mapId, out var name))
            return name;

        return Robust.Shared.Localization.Loc.GetString("alert-level-sector-unknown");
    }

    public void DeleteStationsOnMap(MapId mapId)
    {
        _station.DeleteStationsOnMap(mapId);
    }

    public bool TryDeleteSector(string configId, out string? error)
    {
        PruneDeadInstances();
        error = null;
        if (!_instances.TryGetValue(configId, out var inst))
        {
            error = $"Sector '{configId}' is not loaded";
            return false;
        }

        UnloadSectorInstance(configId, inst);
        return true;
    }

    private void UnloadSectorInstance(string configId, SectorInstance inst)
    {
        _instances.Remove(configId);
        if (_hubSectorId == configId)
            _hubSectorId = null;

        _station.DeleteStationsOnMap(inst.MapId);

        if (inst.StationGrid.IsValid() && Exists(inst.StationGrid) && !TerminatingOrDeleted(inst.StationGrid))
            QueueDel(inst.StationGrid);

        if (_map.MapExists(inst.MapId))
            _map.DeleteMap(inst.MapId);

        RaiseLocalEvent(new SectorUnloadedEvent(configId, inst.MapId));
    }

    private void AbortBootstrap()
    {
        foreach (var (id, kv) in _instances.ToList())
            UnloadSectorInstance(id, kv);

        _instances.Clear();
        _hubSectorId = null;
        _startedThisRound = false;
        _bootstrapComplete = false;
        CentComStarUnlocked = false;
        EmergencyShuttleIndex = 0f;
    }

    private static int GetSectorStartOrder(StarDefinition star)
    {
        if (IsCentComStar(star))
            return 0;
        if (star.IsHub)
            return 1;
        return 2;
    }

    private static bool IsCentComStar(StarDefinition star)
    {
        if (string.Equals(star.Station, "CentComm", StringComparison.OrdinalIgnoreCase))
            return true;

        if (star.FtlWhitelist == null)
            return false;

        foreach (var entry in star.FtlWhitelist)
        {
            if (string.Equals(entry, "AllowFtlToCentCom", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsAsteroidSector(StarDefinition star)
    {
        return string.Equals(star.Id, "AsteroidSectorDefault", StringComparison.OrdinalIgnoreCase)
               || string.Equals(star.Station, "AsteroidTradeOutpost", StringComparison.OrdinalIgnoreCase)
               || string.Equals(star.Station, "Beacon", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsPresetAllowed(StarDefinition star, string? currentPreset)
    {
        if (string.IsNullOrEmpty(star.RequiredGamePreset)
            && (star.RequiredGamePresets == null || star.RequiredGamePresets.Length == 0))
            return true;

        if (string.IsNullOrEmpty(currentPreset))
            return false;

        if (!string.IsNullOrEmpty(star.RequiredGamePreset)
            && string.Equals(star.RequiredGamePreset, currentPreset, StringComparison.Ordinal))
            return true;

        return star.RequiredGamePresets != null && star.RequiredGamePresets.Contains(currentPreset);
    }

    private void OnGenericSectorStartup(Entity<SectorComponent> ent, ref ComponentStartup args)
    {
        if (!ent.Comp.Enabled)
            return;
        if (ent.Comp.Configs != null && ent.Comp.Configs.Count > 0)
        {
            foreach (var cfg in ent.Comp.Configs)
            {
                if (!string.IsNullOrWhiteSpace(cfg))
                    EnsureSector(cfg);
            }
            return;
        }

        if (!string.IsNullOrWhiteSpace(ent.Comp.Config))
            EnsureSector(ent.Comp.Config);
    }

    private void OnCleanup(RoundRestartCleanupEvent ev)
    {
        foreach (var (id, kv) in _instances.ToList())
            UnloadSectorInstance(id, kv);

        _instances.Clear();
        _starIndex = null;
        _hubSectorId = null;
        _startedThisRound = false;
        _bootstrapComplete = false;
        CentComStarUnlocked = false;
        EmergencyShuttleIndex = 0f;
    }

    public bool TryGetMapId(string configId, out MapId mapId)
    {
        PruneDeadInstances();
        mapId = MapId.Nullspace;
        if (_instances.TryGetValue(configId, out var inst))
        {
            mapId = inst.MapId;
            return true;
        }

        return false;
    }

    public bool TryGetHubMapId(out MapId mapId)
    {
        mapId = MapId.Nullspace;
        if (_hubSectorId == null)
            return false;
        return TryGetMapId(_hubSectorId, out mapId);
    }

    public MapId ResolveHubMapId()
    {
        return TryGetHubMapId(out var hub) ? hub : MapId.Nullspace;
    }

    public string? GetHubSectorId() => _hubSectorId;

    public bool TryGetSectorId(MapId mapId, out string sectorId)
    {
        PruneDeadInstances();
        foreach (var (id, inst) in _instances)
        {
            if (inst.MapId != mapId)
                continue;
            sectorId = id;
            return true;
        }

        sectorId = string.Empty;
        return false;
    }

    public bool TryGetStationGrid(string configId, out EntityUid stationGrid)
    {
        stationGrid = EntityUid.Invalid;
        if (!_instances.TryGetValue(configId, out var inst))
            return false;
        stationGrid = inst.StationGrid;
        return stationGrid.IsValid();
    }

    public bool TryGetSectorConfig(MapId mapId, out StarDefinition config)
    {
        PruneDeadInstances();
        foreach (var inst in _instances.Values)
        {
            if (inst.MapId == mapId)
            {
                config = inst.Config;
                return true;
            }
        }

        config = default!;
        return false;
    }

    public bool TryGetCentComSectorId(out string sectorId)
    {
        foreach (var (id, inst) in _instances)
        {
            if (inst.Config.FtlWhitelist != null
                && inst.Config.FtlWhitelist.Contains("AllowFtlToCentCom"))
            {
                sectorId = id;
                return true;
            }

            if (string.Equals(inst.Config.Station, "CentComm", StringComparison.OrdinalIgnoreCase))
            {
                sectorId = id;
                return true;
            }
        }

        sectorId = string.Empty;
        return false;
    }

    public bool TryGetCentComMapId(out MapId mapId)
    {
        mapId = MapId.Nullspace;
        if (!TryGetCentComSectorId(out var id))
            return false;
        return TryGetMapId(id, out mapId);
    }

    public bool TryGetCentCom(out EntityUid mapUid, out MapId mapId, out EntityUid grid)
    {
        mapUid = default;
        mapId = MapId.Nullspace;
        grid = default;

        if (!TryGetCentComSectorId(out var id) || !_instances.TryGetValue(id, out var inst))
            return false;

        if (!inst.StationGrid.IsValid() || !Exists(inst.MapUid))
            return false;

        mapUid = inst.MapUid;
        mapId = inst.MapId;
        grid = inst.StationGrid;
        return true;
    }

    public EntityUid? GetCentComMapUid()
    {
        return TryGetCentCom(out var mapUid, out _, out _) ? mapUid : null;
    }

    private void BindStationCentcomm()
    {
        if (!TryGetCentCom(out var mapUid, out var mapId, out var grid))
        {
            Log.Error("[SectorSystem] CentCom sector not available; StationCentcommComponent not bound");
            return;
        }

        _meta.SetEntityName(mapUid, Loc.GetString("map-name-centcomm"));
        EnsureComp<SectorAtmosSupportComponent>(mapUid);

        var q = EntityQueryEnumerator<StationCentcommComponent>();
        while (q.MoveNext(out var station))
        {
            station.MapEntity = mapUid;
            station.Entity = grid;
            station.MapId = mapId;
            station.ShuttleIndex = EmergencyShuttleIndex;
        }

        Log.Info($"[SectorSystem] Bound StationCentcomm → map={mapId} grid={ToPrettyString(grid)}");
    }

    public void UnlockCentComFtl()
    {
        CentComStarUnlocked = true;
        if (!TryGetCentCom(out var mapUid, out _, out _))
            return;

        if (!TryComp<FTLDestinationComponent>(mapUid, out var ftl))
            return;

        ftl.RequireCoordinateDisk = false;
        ftl.BeaconsOnly = false;
        _shuttle.SetFTLWhitelist((mapUid, ftl), null);
    }

    public void LockCentComFtl()
    {
        CentComStarUnlocked = false;
        if (!TryGetCentComSectorId(out var id) || !_instances.TryGetValue(id, out var inst))
            return;

        if (!TryComp<FTLDestinationComponent>(inst.MapUid, out var ftl))
            return;

        ApplyFtlWhitelist((inst.MapUid, ftl), inst.Config.FtlWhitelist);
    }

    public List<MapId> GetDeadDropMapIds()
    {
        var result = new List<MapId>();
        foreach (var inst in _instances.Values)
        {
            if (!inst.Config.DeadDropEnabled)
                continue;
            if (!_map.MapExists(inst.MapId))
                continue;
            result.Add(inst.MapId);
        }

        return result;
    }

    public void EnsureSector(string configId)
    {
        if (!TryEnsureSector(configId, out var error))
        {
            if (!string.IsNullOrEmpty(error))
                Log.Error($"[SectorSystem] {error}");
            return;
        }

        if (_bootstrapComplete)
            RaiseLocalEvent(new SectorLoadedEvent(configId));
    }

    private bool TryEnsureSector(string configId, out string? error, bool announceProgress = false)
    {
        PruneDeadInstances();
        error = null;
        if (_instances.ContainsKey(configId))
            return true;

        var cfg = FindStar(configId);
        if (cfg == null)
        {
            error = $"Star definition '{configId}' not found in StarmapData";
            return false;
        }

        if (string.Equals(cfg.StarType, "decorative", StringComparison.OrdinalIgnoreCase))
        {
            error = $"Refusing to EnsureSector decorative star '{configId}'";
            return false;
        }

        if (string.IsNullOrEmpty(cfg.Station))
        {
            error = $"Star '{configId}' has no station defined";
            return false;
        }

        Log.Info($"[SectorSystem] EnsureSector begin id='{configId}' name='{cfg.Name}' station='{cfg.Station}'");
        var preset = _ticker.CurrentPreset?.ID;
        if (!IsPresetAllowed(cfg, preset))
        {
            error = $"Star '{configId}' is not allowed for preset '{preset ?? "<null>"}'";
            return false;
        }

        if (!_protos.TryIndex<GameMapPrototype>(cfg.Station, out var gameMap))
        {
            error = $"GameMap '{cfg.Station}' not found for sector '{configId}'";
            return false;
        }

        var sectorName = GetSectorStartupName(cfg);

        try
        {
            var mapUid = _map.CreateMap(out var mapId, false);
            var opts = Robust.Shared.EntitySerialization.DeserializationOptions.Default with { InitializeMaps = true };
            var grids = _ticker.MergeGameMap(gameMap, mapId, opts);
            var stationGrid = grids.FirstOrNull(HasComp<StationMemberComponent>)
                ?? grids.FirstOrNull(HasComp<BecomesStationComponent>)
                ?? grids.FirstOrNull()
                ?? EntityUid.Invalid;

            if (!stationGrid.IsValid())
            {
                if (_map.MapExists(mapId))
                    _map.DeleteMap(mapId);
                error = $"Sector '{configId}' loaded map '{cfg.Station}' but no station grid was found";
                return false;
            }

            if (announceProgress)
                AnnounceStartup(Loc.GetString("sector-startup-generating", ("sector", sectorName)));

            _meta.SetEntityName(mapUid, cfg.Name);
            EnsureComp<SectorAtmosSupportComponent>(mapUid);
            EnsureComp<StationSectorServiceHostComponent>(stationGrid);
            if (cfg.DeadDropEnabled)
            {
                EnsureComp<StationDeadDropComponent>(stationGrid);
                var deadDropComp = Comp<StationDeadDropComponent>(stationGrid);
                deadDropComp.MaxDeadDrops = cfg.DeadDropCount;
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

            if (cfg.IsHub)
                _hubSectorId = configId;

            var poiGroupCount = cfg.POIGroups.Length;
            if (announceProgress && poiGroupCount > 0)
                AnnounceStartup(Loc.GetString("sector-startup-pois", ("count", poiGroupCount)));

            Log.Info($"[SectorSystem] Generating POIs for '{configId}'... groups={poiGroupCount} [{string.Join(',', cfg.POIGroups.Select(g => g.Group))}]");
            GeneratePOIs(mapId, mapUid, cfg, out _);
            if (cfg.AddFtlDestination)
            {
                if (_shuttle.TryAddFTLDestination(mapId, true, false, false, out var ftl))
                    ApplyFtlWhitelist((ftl.Owner, ftl), cfg.FtlWhitelist);
            }

            _map.InitializeMap(mapUid);

            var worldgen = cfg.EnumerateWorldgenConfigs().ToArray();
            if (worldgen.Length > 0)
            {
                EntityManager.System<StarmapWorldgenSystem>().TryApplyWorldgen(
                    mapUid,
                    announceProgress ? AnnounceStartup : null,
                    worldgen);
            }

            if (announceProgress)
                AnnounceStartup(Loc.GetString("sector-startup-done", ("sector", sectorName)));

            Log.Info($"[SectorSystem] EnsureSector done id='{configId}' map='{mapId}'");
            return true;
        }
        catch (Exception e)
        {
            error = $"Exception while loading sector '{configId}' ({cfg.Station}): {e.Message}";
            Log.Error($"[SectorSystem] {error}\n{e}");
            return false;
        }
    }

    private void ApplyFtlWhitelist(Entity<FTLDestinationComponent?> ent, string[]? components)
    {
        if (components == null || components.Length == 0)
        {
            _shuttle.SetFTLWhitelist(ent, null);
            return;
        }

        var whitelist = new EntityWhitelist
        {
            RequireAll = false,
            Components = components
        };
        _shuttle.SetFTLWhitelist(ent, whitelist);
    }

    private void GeneratePOIs(MapId mapId, EntityUid mapUid, StarDefinition cfg, out List<EntityUid> spawnedPOIs)
    {
        spawnedPOIs = new List<EntityUid>();
        var preset = _ticker.CurrentPreset?.ID ?? string.Empty;
        var defaultPreset = cfg.DefaultGamePreset ?? string.Empty;
        var inst = _instances.Values.FirstOrDefault(i => i.MapId == mapId);
        if (inst == null)
            return;

        foreach (var group in cfg.POIGroups)
        {
            var candidates = new List<PointOfInterestPrototype>();
            foreach (var location in _protos.EnumeratePrototypes<PointOfInterestPrototype>())
            {
                if (location.SpawnGroup != group.Group)
                    continue;
                if (location.SpawnGamePreset.Length > 0)
                {
                    var ok = false;
                    if (preset.Length > 0 && location.SpawnGamePreset.Contains(preset))
                        ok = true;
                    else if (defaultPreset.Length > 0 && location.SpawnGamePreset.Contains(defaultPreset))
                        ok = true;
                    if (!ok)
                        continue;
                }

                candidates.Add(location);
            }

            if (candidates.Count == 0)
            {
                Log.Warning($"[SectorSystem] No POI candidates for group '{group.Group}' preset='{preset}'");
                continue;
            }

            if (group.Count <= 0)
            {
                foreach (var proto in candidates)
                {
                    var offset = GetRandomCoord(inst, proto.MinimumDistance, proto.MaximumDistance);
                    if (TrySpawnPoiGrid(mapId, proto, offset, out var gridUid) && gridUid.HasValue)
                        spawnedPOIs.Add(gridUid.Value);
                }

                continue;
            }

            if (group.Ring)
            {
                var rotation = 2 * Math.PI / group.Count;
                var rotationOffset = _rng.NextAngle() / group.Count;
                for (var i = 0; i < group.Count; i++)
                {
                    var proto = _rng.Pick(candidates);
                    Vector2i offset = new Vector2i(_rng.Next(proto.MinimumDistance, proto.MaximumDistance), 0);
                    offset = offset.Rotate(rotationOffset);
                    rotationOffset += rotation;
                    var overrideName = proto.Name + (i < 26 ? $" {(char)('A' + i)}" : $" {i + 1}");
                    if (TrySpawnPoiGrid(mapId, proto, offset, out var gridUid, overrideName) && gridUid.HasValue)
                    {
                        var depotStation = _station.GetOwningStation(gridUid.Value);
                        if (TryComp<TradeCrateDestinationComponent>(depotStation, out var destComp))
                            destComp.DestinationProto = i < 26 ? $"Cargo{(char)('A' + i)}" : "CargoOther";
                        spawnedPOIs.Add(gridUid.Value);
                        Log.Info($"[SectorSystem] Spawned POI '{proto.ID}' as '{overrideName}' at {offset}");
                    }
                }
            }
            else
            {
                _rng.Shuffle(candidates);
                var spawned = 0;
                foreach (var proto in candidates)
                {
                    if (spawned >= group.Count)
                        break;
                    var offset = GetRandomCoord(inst, proto.MinimumDistance, proto.MaximumDistance);
                    if (TrySpawnPoiGrid(mapId, proto, offset, out var gridUid) && gridUid.HasValue)
                    {
                        spawnedPOIs.Add(gridUid.Value);
                        Log.Info($"[SectorSystem] Spawned POI '{proto.ID}' at {offset}");
                        spawned++;
                    }
                }
            }
        }
    }

    private bool TrySpawnPoiGrid(MapId mapUid, PointOfInterestPrototype proto, Vector2 offset, out EntityUid? gridUid, string? overrideName = null)
    {
        gridUid = null;
        if (!_loader.TryLoadGrid(mapUid, proto.GridPath, out var loadedGrid, offset: offset, rot: _rng.NextAngle()))
        {
            Log.Warning($"[SectorSystem] Failed to load POI grid '{proto.GridPath}' for '{proto.ID}'");
            return false;
        }

        gridUid = loadedGrid.Value;
        List<EntityUid> gridList = new() { loadedGrid.Value };
        var stationName = string.IsNullOrEmpty(overrideName) ? proto.Name : overrideName;
        EntityUid? stationUid = null;
        if (_protos.TryIndex<GameMapPrototype>(proto.ID, out var stationProto))
            stationUid = _station.InitializeNewStation(stationProto.Stations[proto.ID], gridList, stationName);
        var meta = EnsureComp<MetaDataComponent>(loadedGrid.Value);
        _meta.SetEntityName(loadedGrid.Value, stationName, meta);
        EntityManager.AddComponents(loadedGrid.Value, proto.AddComponents);

        if (proto.NameWarp)
        {
            bool? hideWarp = proto.HideWarp ? true : null;
            if (stationUid != null)
                _renameWarps.SyncWarpPointsToStation(stationUid.Value, forceAdminOnly: hideWarp);
            else
                _renameWarps.SyncWarpPointsToGrids(gridList, forceAdminOnly: hideWarp);
        }

        return true;
    }

    private Vector2 GetRandomCoord(SectorInstance inst, float minRange, float maxRange)
    {
        var coords = _rng.NextVector2(minRange, maxRange);
        for (var i = 0; i < 8; i++)
        {
            var valid = true;
            foreach (var taken in inst.OccupiedPoiCoords)
            {
                if (Vector2.Distance(taken, coords) < minRange * 0.5f)
                {
                    valid = false;
                    break;
                }
            }

            if (valid)
                break;
            coords = _rng.NextVector2(minRange, maxRange);
        }

        inst.OccupiedPoiCoords.Add(coords);
        return coords;
    }
}
