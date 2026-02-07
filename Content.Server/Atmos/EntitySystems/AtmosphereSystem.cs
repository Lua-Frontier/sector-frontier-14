using Content.Server.Administration.Logs;
using Content.Server.Atmos.Components;
using Content.Server.Body.Systems;
using Content.Server.Fluids.EntitySystems;
using Content.Server.GameTicking; // Frontier
using Content.Server.NodeContainer.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Decals;
using Content.Shared.Doors.Components;
using Content.Shared.Maps;
using JetBrains.Annotations;
using Prometheus;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Server.Atmos.EntitySystems;

/// <summary>
///     This is our SSAir equivalent, if you need to interact with or query atmos in any way, go through this.
/// </summary>
[UsedImplicitly]
public sealed partial class AtmosphereSystem : SharedAtmosphereSystem
{
    private static readonly Gauge AtmosGridsGauge = Metrics.CreateGauge(
        "atmos_grids",
        "Number of grids with GridAtmosphereComponent.");

    private static readonly Gauge AtmosTilesGauge = Metrics.CreateGauge(
        "atmos_tiles_total",
        "Total number of atmos tiles across all grids.");

    private static readonly Gauge AtmosActiveTilesGauge = Metrics.CreateGauge(
        "atmos_tiles_active",
        "Total number of active atmos tiles across all grids.");

    private static readonly Gauge AtmosInvalidatedCoordsGauge = Metrics.CreateGauge(
        "atmos_invalidated_coords",
        "Total number of invalidated coords across all grids.");

    private static readonly Gauge AtmosExcitedGroupsGauge = Metrics.CreateGauge(
        "atmos_excited_groups",
        "Total number of excited groups across all grids.");

    private static readonly Gauge AtmosHotspotTilesGauge = Metrics.CreateGauge(
        "atmos_hotspot_tiles",
        "Total number of hotspot tiles across all grids.");

    private static readonly Gauge AtmosSuperconductivityTilesGauge = Metrics.CreateGauge(
        "atmos_superconductivity_tiles",
        "Total number of superconductivity tiles across all grids.");

    private static readonly Gauge AtmosHighPressureDeltaGauge = Metrics.CreateGauge(
        "atmos_high_pressure_delta_tiles",
        "Total number of tiles in high-pressure-delta set across all grids.");

    private float _metricsTimer;
    private const float MetricsInterval = 30f;

    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefinitionManager = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly GasTileOverlaySystem _gasTileOverlaySystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly TileSystem _tile = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] public readonly PuddleSystem Puddle = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!; // Frontier

    private const float ExposedUpdateDelay = 1f;
    private float _exposedTimer = 0f;

    private EntityQuery<GridAtmosphereComponent> _atmosQuery;
    private EntityQuery<MapAtmosphereComponent> _mapAtmosQuery;
    private EntityQuery<AirtightComponent> _airtightQuery;
    private EntityQuery<FirelockComponent> _firelockQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<MetaDataComponent> _metaQuery;
    private EntityQuery<MovedByPressureComponent> _movedByPressureQuery;
    private HashSet<EntityUid> _entSet = new();

    private string[] _burntDecals = [];

    public override void Initialize()
    {
        base.Initialize();

        UpdatesAfter.Add(typeof(NodeGroupSystem));

        InitializeGases();
        InitializeCommands();
        InitializeCVars();
        InitializeGridAtmosphere();
        InitializeMap();

        _mapAtmosQuery = GetEntityQuery<MapAtmosphereComponent>();
        _atmosQuery = GetEntityQuery<GridAtmosphereComponent>();
        _airtightQuery = GetEntityQuery<AirtightComponent>();
        _firelockQuery = GetEntityQuery<FirelockComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
        _metaQuery = GetEntityQuery<MetaDataComponent>();
        _movedByPressureQuery = GetEntityQuery<MovedByPressureComponent>();

        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        SubscribeLocalEvent<GridAtmosphereComponent, ComponentShutdown>(OnGridAtmosphereShutdown);

        CacheDecals();
    }

    public override void Shutdown()
    {
        base.Shutdown();

        ShutdownCommands();
    }

    private void OnTileChanged(ref TileChangedEvent ev)
    {
        foreach (var change in ev.Changes)
        {
            InvalidateTile(ev.Entity.Owner, change.GridIndices);
        }
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs ev)
    {
        if (ev.WasModified<DecalPrototype>())
            CacheDecals();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateProcessing(frameTime);
        UpdateHighPressure(frameTime);

        _metricsTimer += frameTime;
        if (_metricsTimer >= MetricsInterval)
        {
            _metricsTimer -= MetricsInterval;
            UpdateAtmosMetrics();
        }

        _exposedTimer += frameTime;

        if (_exposedTimer < ExposedUpdateDelay)
            return;

        var query = EntityQueryEnumerator<AtmosExposedComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var transform))
        {
            var air = GetContainingMixture((uid, transform));

            if (air == null)
                continue;

            var updateEvent = new AtmosExposedUpdateEvent(transform.Coordinates, air, transform);
            RaiseLocalEvent(uid, ref updateEvent);
        }

        _exposedTimer -= ExposedUpdateDelay;
    }

    private void CacheDecals()
    {
        _burntDecals = _protoMan.EnumeratePrototypes<DecalPrototype>().Where(x => x.Tags.Contains("burnt")).Select(x => x.ID).ToArray();
    }

    private void UpdateAtmosMetrics()
    {
        var grids = 0;
        var tiles = 0;
        var activeTiles = 0;
        var invalid = 0;
        var excitedGroups = 0;
        var hotspots = 0;
        var superconduction = 0;
        var highPressure = 0;

        var query = EntityQueryEnumerator<GridAtmosphereComponent>();
        while (query.MoveNext(out _, out var atmos))
        {
            grids++;
            tiles += atmos.Tiles.Count;
            activeTiles += atmos.ActiveTiles.Count;
            invalid += atmos.InvalidatedCoords.Count;
            excitedGroups += atmos.ExcitedGroups.Count;
            hotspots += atmos.HotspotTiles.Count;
            superconduction += atmos.SuperconductivityTiles.Count;
            highPressure += atmos.HighPressureDelta.Count;
        }

        AtmosGridsGauge.Set(grids);
        AtmosTilesGauge.Set(tiles);
        AtmosActiveTilesGauge.Set(activeTiles);
        AtmosInvalidatedCoordsGauge.Set(invalid);
        AtmosExcitedGroupsGauge.Set(excitedGroups);
        AtmosHotspotTilesGauge.Set(hotspots);
        AtmosSuperconductivityTilesGauge.Set(superconduction);
        AtmosHighPressureDeltaGauge.Set(highPressure);
    }

    private void OnGridAtmosphereShutdown(EntityUid uid, GridAtmosphereComponent component, ComponentShutdown args)
    {
        foreach (var group in component.ExcitedGroups)
        {
            group.Tiles.Clear();
            group.Disposed = true;
        }

        foreach (var tile in component.Tiles.Values)
        {
            tile.ExcitedGroup = null;
            tile.Excited = false;
            tile.PressureSpecificTarget = null;
            tile.Air = null;
            tile.AirArchived = null;

            for (var i = 0; i < tile.AdjacentTiles.Length; i++)
            {
                tile.AdjacentTiles[i] = null;
            }

            tile.AdjacentBits = AtmosDirection.Invalid;
        }

        component.CurrentRunTiles.Clear();
        component.CurrentRunExcitedGroups.Clear();
        component.CurrentRunPipeNet.Clear();
        component.CurrentRunAtmosDevices.Clear();
        component.CurrentRunInvalidatedTiles.Clear();

        component.InvalidatedCoords.Clear();
        component.PossiblyDisconnectedTiles.Clear();
        component.ActiveTiles.Clear();
        component.ExcitedGroups.Clear();
        component.HotspotTiles.Clear();
        component.SuperconductivityTiles.Clear();
        component.HighPressureDelta.Clear();
        component.PipeNets.Clear();
        component.AtmosDevices.Clear();
        component.MapTiles.Clear();
        component.Tiles.Clear();
    }
}
