using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Content.Server.Atmos;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Decals;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Server._Lua.MapperGrid; // Lua
using Content.Server._Lua.Stargate.Components;
using Content.Shared.Atmos;
using Content.Shared.Decals;
using Content.Shared.Ghost;
using Content.Shared.Gravity;
using Content.Shared.Light.Components;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Parallax.Biomes.Layers;
using Content.Shared.Parallax.Biomes.Markers;
using Content.Shared.CCVar;
using Content.Shared.Salvage; // Lua
using Content.Shared.Tag;
using Microsoft.Extensions.ObjectPool;
using Robust.Server.Player;
using Robust.Shared;
using Robust.Shared.Collections;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Threading;
using Robust.Shared.Utility;
using ChunkIndicesEnumerator = Robust.Shared.Map.Enumerators.ChunkIndicesEnumerator;

namespace Content.Server.Parallax;

public sealed partial class BiomeSystem : SharedBiomeSystem
{
    [Dependency] private readonly IConfigurationManager _configManager = default!;
    [Dependency] private readonly IConsoleHost _console = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IParallelManager _parallel = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly DecalSystem _decals = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ShuttleSystem _shuttles = default!;
    [Dependency] private readonly TagSystem _tags = default!;

    private EntityQuery<BiomeComponent> _biomeQuery;
    private EntityQuery<FixturesComponent> _fixturesQuery;
    private EntityQuery<GhostComponent> _ghostQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    private readonly HashSet<EntityUid> _handledEntities = new();
    private const float DefaultLoadRange = 16f;
    private float _loadRange = DefaultLoadRange;
    private int _chunkBudget = 3;
    private int _markerBudget = 20;
    private int _markerChunkBudget = 2;
    private int _decalBudget = 21;
    private int _entityBudget = 21;
    private static readonly ProtoId<TagPrototype> AllowBiomeLoadingTag = "AllowBiomeLoading";
    private readonly Dictionary<EntityUid, float> _stargateMapMotion = new();
    private readonly Dictionary<EntityUid, (EntityUid mapUid, Vector2 worldPos)> _observerLastPosition = new();
    private readonly HashSet<EntityUid> _observersSeenThisTick = new();
    private readonly HashSet<EntityUid> _stargateHardPauseMaps = new();
    private long _nextOreVeinWarningTickMs;
    private const long OreVeinWarningIntervalMs = 5000;

    private ObjectPool<HashSet<Vector2i>> _tilePool =
        new DefaultObjectPool<HashSet<Vector2i>>(new SetPolicy<Vector2i>(), 256);

    /// <summary>
    /// Load area for chunks containing tiles, decals etc.
    /// </summary>
    private Box2 _loadArea = new(-DefaultLoadRange, -DefaultLoadRange, DefaultLoadRange, DefaultLoadRange);

    private EntityQuery<RestrictedRangeComponent> _restrictedQuery;

    /// <summary>
    /// Stores the chunks active for this tick temporarily.
    /// </summary>
    private readonly Dictionary<BiomeComponent, HashSet<Vector2i>> _activeChunks = new();

    private readonly Dictionary<BiomeComponent,
        Dictionary<string, HashSet<Vector2i>>> _markerChunks = new();

    private readonly List<Vector2i> _unloadChunksBuffer = new();
    private readonly List<(Vector2i, Tile)> _unloadTilesBuffer = new();

    /// <summary>
    /// Components that change at spawn or during simulation but do not indicate player interaction.
    /// Ignored when checking if a biome-spawned entity can be safely deleted on chunk unload.
    /// </summary>
    private static readonly HashSet<string> BiomeUnloadIgnoredComponents = new()
    {
        "Physics",
        "Fixtures",
        "Sprite",
        "RandomSprite",
        // Floor water/lava/plasma: solution structure changes at MapInit, emission/tile state
        "SolutionContainerManager",
        "DrainableSolution",
        "TileEmission",
        // Floor water: contacts, triggers, occlusion, footstep, fishing
        "SpeedModifierContacts",
        "StepTrigger",
        "FloorOccluder",
        "FootstepModifier",
        "TileEntityEffect",
        "FishingSpot"
    };

    public override void Initialize()
    {
        base.Initialize();
        _biomeQuery = GetEntityQuery<BiomeComponent>();
        _fixturesQuery = GetEntityQuery<FixturesComponent>();
        _ghostQuery = GetEntityQuery<GhostComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
        _restrictedQuery = GetEntityQuery<RestrictedRangeComponent>();
        SubscribeLocalEvent<BiomeComponent, MapInitEvent>(OnBiomeMapInit);
        SubscribeLocalEvent<FTLStartedEvent>(OnFTLStarted);
        SubscribeLocalEvent<ShuttleFlattenEvent>(OnShuttleFlatten);
        Subs.CVar(_configManager, CCVars.BiomeLoadRange, SetLoadRange, true);
        Subs.CVar(_configManager, CCVars.BiomeChunkBudget, v => _chunkBudget = v, true);
        Subs.CVar(_configManager, CCVars.BiomeMarkerBudget, v => _markerBudget = v, true);
        Subs.CVar(_configManager, CCVars.BiomeMarkerChunkBudget, v => _markerChunkBudget = v, true);
        Subs.CVar(_configManager, CCVars.BiomeDecalBudget, v => _decalBudget = v, true);
        Subs.CVar(_configManager, CCVars.BiomeEntityBudget, v => _entityBudget = v, true);
        InitializeCommands();
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(ProtoReload);
    }

    private void ProtoReload(PrototypesReloadedEventArgs obj)
    {
        ClearNoiseCache();

        if (!obj.ByType.TryGetValue(typeof(BiomeTemplatePrototype), out var reloads))
            return;

        var query = AllEntityQuery<BiomeComponent>();

        while (query.MoveNext(out var uid, out var biome))
        {
            if (biome.Template == null || !reloads.Modified.TryGetValue(biome.Template, out var proto))
                continue;

            SetTemplate(uid, biome, (BiomeTemplatePrototype)proto);
        }
    }

    private void SetLoadRange(float obj)
    {
        _loadRange = obj;
        _loadArea = new Box2(-_loadRange, -_loadRange, _loadRange, _loadRange);
    }

    private void OnBiomeMapInit(EntityUid uid, BiomeComponent component, MapInitEvent args)
    {
        if (component.Seed == -1)
        {
            SetSeed(uid, component, _random.Next());
        }

        if (_proto.TryIndex(component.Template, out var biome))
            SetTemplate(uid, component, biome);

        var xform = Transform(uid);
        var mapId = xform.MapID;

        if (mapId != MapId.Nullspace && HasComp<MapGridComponent>(uid))
        {
            var setTiles = new List<(Vector2i Index, Tile tile)>();

            foreach (var grid in _mapManager.GetAllGrids(mapId))
            {
                if (!_fixturesQuery.TryGetComponent(grid.Owner, out var fixtures))
                    continue;

                // Don't want shuttles flying around now do we.
                _shuttles.Disable(grid.Owner);
                var pTransform = _physics.GetPhysicsTransform(grid.Owner);

                foreach (var fixture in fixtures.Fixtures.Values)
                {
                    for (var i = 0; i < fixture.Shape.ChildCount; i++)
                    {
                        var aabb = fixture.Shape.ComputeAABB(pTransform, i);

                        setTiles.Clear();
                        ReserveTiles(uid, aabb, setTiles);
                    }
                }
            }
        }
    }

    public void SetEnabled(Entity<BiomeComponent?> ent, bool enabled = true)
    {
        if (!Resolve(ent, ref ent.Comp) || ent.Comp.Enabled == enabled)
            return;

        ent.Comp.Enabled = enabled;
        Dirty(ent, ent.Comp);
    }

    public void SetSeed(EntityUid uid, BiomeComponent component, int seed, bool dirty = true)
    {
        component.Seed = seed;

        if (dirty)
            Dirty(uid, component);
    }

    public void ClearTemplate(EntityUid uid, BiomeComponent component, bool dirty = true)
    {
        component.Layers.Clear();
        component.Template = null;

        if (dirty)
            Dirty(uid, component);
    }

    /// <summary>
    /// Sets the <see cref="BiomeComponent.Template"/> and refreshes layers.
    /// </summary>
    public void SetTemplate(EntityUid uid, BiomeComponent component, BiomeTemplatePrototype template, bool dirty = true)
    {
        component.Layers.Clear();
        component.Template = template.ID;

        foreach (var layer in template.Layers)
        {
            component.Layers.Add(layer);
        }

        if (dirty)
            Dirty(uid, component);
    }

    /// <summary>
    /// Adds the specified layer at the specified marker if it exists.
    /// </summary>
    public void AddLayer(EntityUid uid, BiomeComponent component, string id, IBiomeLayer addedLayer, int seedOffset = 0)
    {
        for (var i = 0; i < component.Layers.Count; i++)
        {
            var layer = component.Layers[i];

            if (layer is not BiomeDummyLayer dummy || dummy.ID != id)
                continue;

            addedLayer.Noise.SetSeed(addedLayer.Noise.GetSeed() + seedOffset);
            component.Layers.Insert(i, addedLayer);
            break;
        }

        Dirty(uid, component);
    }

    public void AddMarkerLayer(EntityUid uid, BiomeComponent component, string marker)
    {
        component.MarkerLayers.Add(marker);
        Dirty(uid, component);
    }

    /// <summary>
    /// Adds the specified template at the specified marker if it exists, withour overriding every layer.
    /// </summary>
    public void AddTemplate(EntityUid uid, BiomeComponent component, string id, BiomeTemplatePrototype template, int seedOffset = 0)
    {
        for (var i = 0; i < component.Layers.Count; i++)
        {
            var layer = component.Layers[i];

            if (layer is not BiomeDummyLayer dummy || dummy.ID != id)
                continue;

            for (var j = template.Layers.Count - 1; j >= 0; j--)
            {
                var addedLayer = template.Layers[j];
                addedLayer.Noise.SetSeed(addedLayer.Noise.GetSeed() + seedOffset);
                component.Layers.Insert(i, addedLayer);
            }

            break;
        }

        Dirty(uid, component);
    }

    private void OnFTLStarted(ref FTLStartedEvent ev)
    {
        var targetMap = _transform.ToMapCoordinates(ev.TargetCoordinates);
        var targetMapUid = _mapSystem.GetMapOrInvalid(targetMap.MapId);

        if (!TryComp<BiomeComponent>(targetMapUid, out var biome))
            return;

        var preloadArea = new Vector2(32f, 32f);
        var targetArea = new Box2(targetMap.Position - preloadArea, targetMap.Position + preloadArea);
        Preload(targetMapUid, biome, targetArea);
    }

    private void OnShuttleFlatten(ref ShuttleFlattenEvent ev)
    {
        if (!TryComp<BiomeComponent>(ev.MapUid, out var biome) ||
            !TryComp<MapGridComponent>(ev.MapUid, out var grid))
        {
            return;
        }

        var tiles = new List<(Vector2i Index, Tile Tile)>();

        foreach (var aabb in ev.AABBs)
        {
            for (var x = Math.Floor(aabb.Left); x <= Math.Ceiling(aabb.Right); x++)
            {
                for (var y = Math.Floor(aabb.Bottom); y <= Math.Ceiling(aabb.Top); y++)
                {
                    var index = new Vector2i((int)x, (int)y);
                    var chunk = SharedMapSystem.GetChunkIndices(index, ChunkSize);

                    var mod = biome.ModifiedTiles.GetOrNew(chunk * ChunkSize);

                    if (!mod.Add(index) || !TryGetBiomeTile(index, biome.Layers, biome.Seed, (ev.MapUid, grid), out var tile))
                        continue;

                    // If we flag it as modified then the tile is never set so need to do it ourselves.
                    tiles.Add((index, tile.Value));
                }
            }
        }

        _mapSystem.SetTiles(ev.MapUid, grid, tiles);
    }

    /// <summary>
    /// Preloads biome for the specified area.
    /// </summary>
    public void Preload(EntityUid uid, BiomeComponent component, Box2 area)
    {
        var markers = component.MarkerLayers;
        var goobers = _markerChunks.GetOrNew(component);

        foreach (var layer in markers)
        {
            var proto = ProtoManager.Index(layer);
            var enumerator = new ChunkIndicesEnumerator(area, proto.Size);

            while (enumerator.MoveNext(out var chunk))
            {
                var chunkOrigin = chunk * proto.Size;
                var layerChunks = goobers.GetOrNew(proto.ID);
                layerChunks.Add(chunkOrigin.Value);
            }
        }
    }

    private bool CanLoad(EntityUid uid)
    {
        return !_ghostQuery.HasComp(uid) || _tags.HasTag(uid, AllowBiomeLoadingTag);
    }

    private bool IsStargateBiomeMap(EntityUid mapUid, out StargateDestinationComponent? destination)
    {
        if (TryComp<StargateDestinationComponent>(mapUid, out var dest))
        {
            destination = dest;
            return true;
        }

        destination = null;
        return false;
    }

    private void TrackObserverMotion(EntityUid observer, EntityUid mapUid, Vector2 worldPos)
    {
        _observersSeenThisTick.Add(observer);

        if (!IsStargateBiomeMap(mapUid, out _))
        {
            _observerLastPosition[observer] = (mapUid, worldPos);
            return;
        }

        if (_observerLastPosition.TryGetValue(observer, out var last) && last.mapUid == mapUid)
        {
            var delta = Vector2.Distance(last.worldPos, worldPos);
            if (delta > 0f)
                _stargateMapMotion[mapUid] = _stargateMapMotion.GetValueOrDefault(mapUid) + delta;
        }

        _observerLastPosition[observer] = (mapUid, worldPos);
    }

    private static int CountPendingMarkers(BiomeComponent component)
    {
        var total = 0;
        foreach (var (_, layers) in component.PendingMarkers)
        {
            foreach (var (_, nodes) in layers)
            {
                total += nodes.Count;
            }
        }

        return total;
    }

    private static int CountPendingDynamicSpawns(BiomeComponent component)
    {
        return component.PendingEntities.Sum(x => x.Value.Count) + component.PendingDecals.Sum(x => x.Value.Count);
    }

    private (int Chunk, int Marker, int Entity, int Decal) GetDynamicBudgets(EntityUid mapUid, BiomeComponent component)
    {
        var chunkBudget = _chunkBudget;
        var markerBudget = _markerBudget;
        var entityBudget = _entityBudget;
        var decalBudget = _decalBudget;

        if (!IsStargateBiomeMap(mapUid, out var destination))
            return (chunkBudget, markerBudget, entityBudget, decalBudget);

        var motion = _stargateMapMotion.GetValueOrDefault(mapUid);
        var pendingCount = component.PendingEntities.Sum(x => x.Value.Count) + component.PendingDecals.Sum(x => x.Value.Count);
        var pendingMarkers = CountPendingMarkers(component);

        var motionFactor = 1f;
        if (motion >= 20f)
            motionFactor = 0.25f;
        else if (motion >= 10f)
            motionFactor = 0.40f;
        else if (motion >= 4f)
            motionFactor = 0.60f;
        else if (motion >= 1f)
            motionFactor = 0.65f;
        else
            motionFactor = 1.15f;

        if (destination != null && destination.ProgressiveLoadingActive)
            motionFactor *= 0.85f;

        var pendingFactor = 1f;
        if (pendingCount >= 500)
            pendingFactor = 0.70f;
        else if (pendingCount >= 250)
            pendingFactor = 0.82f;
        else if (pendingCount <= 60)
            pendingFactor = 1.10f;

        if (pendingMarkers >= 2600)
            pendingFactor *= 0.45f;
        else if (pendingMarkers >= 1800)
            pendingFactor *= 0.60f;
        else if (pendingMarkers >= 1000)
            pendingFactor *= 0.78f;
        else if (pendingMarkers >= 700)
            pendingFactor *= 0.90f;

        var factor = Math.Clamp(motionFactor * pendingFactor, 0.2f, 1.35f);

        if (chunkBudget > 0)
            chunkBudget = Math.Max(1, (int)MathF.Round(chunkBudget * factor));
        if (markerBudget > 0)
            markerBudget = Math.Max(1, (int)MathF.Round(markerBudget * factor));
        if (entityBudget > 0)
            entityBudget = Math.Max(1, (int)MathF.Round(entityBudget * factor));
        if (decalBudget > 0)
            decalBudget = Math.Max(1, (int)MathF.Round(decalBudget * factor));

        // Extra clamp for heavy rock/ore regions while observer is moving fast.
        if (motion >= 10f)
        {
            chunkBudget = Math.Min(chunkBudget, 1);
            markerBudget = Math.Min(markerBudget, 2);
            entityBudget = Math.Min(entityBudget, 4);
            decalBudget = Math.Min(decalBudget, 4);
        }
        else if (motion >= 4f)
        {
            chunkBudget = Math.Min(chunkBudget, 2);
            markerBudget = Math.Min(markerBudget, 4);
            entityBudget = Math.Min(entityBudget, 8);
            decalBudget = Math.Min(decalBudget, 8);
        }

        if (motion >= 1f)
        {
            markerBudget = Math.Min(markerBudget, 2);
            entityBudget = Math.Min(entityBudget, 6);
            decalBudget = Math.Min(decalBudget, 6);
        }

        if (pendingMarkers >= 2200)
        {
            chunkBudget = Math.Min(chunkBudget, 1);
            markerBudget = Math.Min(markerBudget, 1);
            entityBudget = Math.Min(entityBudget, 6);
            decalBudget = Math.Min(decalBudget, 6);
        }
        else if (pendingMarkers >= 1400)
        {
            markerBudget = Math.Min(markerBudget, 1);
            entityBudget = Math.Min(entityBudget, 5);
            decalBudget = Math.Min(decalBudget, 5);
        }
        else if (pendingMarkers >= 900)
        {
            markerBudget = Math.Min(markerBudget, 2);
        }

        return (chunkBudget, markerBudget, entityBudget, decalBudget);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _stargateMapMotion.Clear();
        _observersSeenThisTick.Clear();
        var biomes = AllEntityQuery<BiomeComponent>();

        while (biomes.MoveNext(out var biome))
        {
            if (biome.LifeStage < ComponentLifeStage.Running)
                continue;

            _activeChunks.Add(biome, _tilePool.Get());
            _markerChunks.GetOrNew(biome);
        }

        // Get chunks in range
        foreach (var pSession in Filter.GetAllPlayers(_playerManager))
        {
            if (_xformQuery.TryGetComponent(pSession.AttachedEntity, out var xform) &&
                _handledEntities.Add(pSession.AttachedEntity.Value) &&
                 _biomeQuery.TryGetComponent(xform.MapUid, out var biome) &&
                biome.Enabled &&
                CanLoad(pSession.AttachedEntity.Value))
            {
                var worldPos = _transform.GetWorldPosition(xform);
                if (xform.MapUid is { } mapUid)
                    TrackObserverMotion(pSession.AttachedEntity.Value, mapUid, worldPos);
                AddChunksInRange(biome, worldPos);

                foreach (var layer in biome.MarkerLayers)
                {
                    var layerProto = ProtoManager.Index(layer);
                    AddMarkerChunksInRange(biome, worldPos, layerProto);
                }
            }

            foreach (var viewer in pSession.ViewSubscriptions)
            {
                if (!_handledEntities.Add(viewer) ||
                    !_xformQuery.TryGetComponent(viewer, out xform) ||
                    !_biomeQuery.TryGetComponent(xform.MapUid, out biome) ||
                    !biome.Enabled ||
                    !CanLoad(viewer))
                {
                    continue;
                }

                var worldPos = _transform.GetWorldPosition(xform);
                if (xform.MapUid is { } mapUid)
                    TrackObserverMotion(viewer, mapUid, worldPos);
                AddChunksInRange(biome, worldPos);

                foreach (var layer in biome.MarkerLayers)
                {
                    var layerProto = ProtoManager.Index(layer);
                    AddMarkerChunksInRange(biome, worldPos, layerProto);
                }
            }
        }

        var loadBiomes = AllEntityQuery<BiomeComponent, MapGridComponent>();

        while (loadBiomes.MoveNext(out var gridUid, out var biome, out var grid))
        {
            // If not MapInit don't run it.
            if (biome.LifeStage < ComponentLifeStage.Running)
                continue;

            if (!biome.Enabled)
                continue;

            var budgets = GetDynamicBudgets(gridUid, biome);

            // Load new chunks
            LoadChunks(biome, gridUid, grid, biome.Seed, budgets.Chunk, budgets.Marker, budgets.Entity, budgets.Decal);
            // Unload old chunks
            UnloadChunks(biome, gridUid, grid, biome.Seed, budgets.Chunk);
            ProcessMarkerChunkUnloads(biome);
        }

        _handledEntities.Clear();
        foreach (var observer in _observerLastPosition.Keys.ToList())
        {
            if (!_observersSeenThisTick.Contains(observer))
                _observerLastPosition.Remove(observer);
        }

        foreach (var tiles in _activeChunks.Values)
        {
            _tilePool.Return(tiles);
        }

        _activeChunks.Clear();
        _markerChunks.Clear();
    }

    private void AddChunksInRange(BiomeComponent biome, Vector2 worldPos)
    {
        var enumerator = new ChunkIndicesEnumerator(_loadArea.Translated(worldPos), ChunkSize);

        while (enumerator.MoveNext(out var chunkOrigin))
        {
            _activeChunks[biome].Add(chunkOrigin.Value * ChunkSize);
        }
    }

    private void AddMarkerChunksInRange(BiomeComponent biome, Vector2 worldPos, IBiomeMarkerLayer layer)
    {
        // Offset the load area so it's centralised.
        var loadArea = new Box2(0, 0, layer.Size, layer.Size);
        var halfLayer = new Vector2(layer.Size / 2f);

        var enumerator = new ChunkIndicesEnumerator(loadArea.Translated(worldPos - halfLayer), layer.Size);

        var lay = _markerChunks[biome].GetOrNew(layer.ID);
        while (enumerator.MoveNext(out var chunkOrigin))
        {
            lay.Add(chunkOrigin.Value * layer.Size);
        }
    }

    #region Load

    private sealed class PreparedChunkData
    {
        public Vector2i Chunk;
        public readonly List<(Vector2i Index, Tile Tile)> Tiles = new();
        public readonly List<(Vector2i Index, string Prototype)> Entities = new();
        public readonly List<(Vector2i Index, string DecalId, Vector2 Position)> Decals = new();
    }

    /// <summary>
    /// Loads all of the chunks for a particular biome, as well as handle any marker chunks.
    /// </summary>
    private void LoadChunks(
        BiomeComponent component,
        EntityUid gridUid,
        MapGridComponent grid,
        int seed,
        int chunkBudget,
        int markerBudget,
        int entityBudget,
        int decalBudget)
    {
        BuildMarkerChunks(component, gridUid, grid, seed);

        _restrictedQuery.TryGetComponent(gridUid, out var restricted);

        var active = _activeChunks[component];

        var disableMarkerLoading = false;
        if (IsStargateBiomeMap(gridUid, out _))
        {
            var motion = _stargateMapMotion.GetValueOrDefault(gridUid);
            var pendingMarkers = CountPendingMarkers(component);
            var pendingDynamic = CountPendingDynamicSpawns(component);
            disableMarkerLoading = pendingDynamic >= 900 || (motion >= 0.90f && pendingDynamic >= 600);
        }

        if (!disableMarkerLoading)
        {
            var markerBudgetLeft = markerBudget;
            foreach (var chunk in active)
            {
                if (markerBudget > 0 && markerBudgetLeft <= 0)
                    break;
                markerBudgetLeft = LoadChunkMarkers(component, gridUid, grid, chunk, seed, restricted, markerBudgetLeft);
            }
        }

        var chunksToLoad = new List<Vector2i>(chunkBudget);
        foreach (var chunk in active)
        {
            if (component.LoadedChunks.Contains(chunk))
                continue;

            chunksToLoad.Add(chunk);
            if (chunksToLoad.Count >= chunkBudget)
                break;
        }

        var entityBudgetLeft = entityBudget;
        if (entityBudget > 0 && component.PendingEntities.Count > 0)
        {
            foreach (var (chunk, list) in component.PendingEntities.ToList())
            {
                if (entityBudgetLeft <= 0)
                    break;
                if (!component.LoadedEntities.TryGetValue(chunk, out var loadedEntities))
                    continue;
                for (var i = list.Count - 1; i >= 0 && entityBudgetLeft > 0; i--)
                {
                    var (indices, prototype) = list[i];
                    var ent = Spawn(prototype, _mapSystem.GridTileToLocal(gridUid, grid, indices));
                    if (_xformQuery.TryGetComponent(ent, out var xform) && !xform.Anchored)
                        _transform.AnchorEntity((ent, xform), (gridUid, grid), indices);
                    loadedEntities.Add(ent, indices);
                    list.RemoveAt(i);
                    entityBudgetLeft--;
                }
                if (list.Count == 0)
                    component.PendingEntities.Remove(chunk);
            }
        }

        var decalBudgetLeft = decalBudget;
        if (decalBudget > 0 && component.PendingDecals.Count > 0)
        {
            foreach (var (chunk, list) in component.PendingDecals.ToList())
            {
                if (decalBudgetLeft <= 0)
                    break;
                if (!component.LoadedDecals.TryGetValue(chunk, out var loadedDecals))
                    continue;
                for (var i = list.Count - 1; i >= 0 && decalBudgetLeft > 0; i--)
                {
                    var (indices, decalId, position) = list[i];
                    if (!_decals.TryAddDecal(decalId, new EntityCoordinates(gridUid, position), out var dec))
                        continue;
                    loadedDecals.Add(dec, indices);
                    list.RemoveAt(i);
                    decalBudgetLeft--;
                }
                if (list.Count == 0)
                    component.PendingDecals.Remove(chunk);
            }
        }

        if (chunksToLoad.Count == 0)
            return;
        var prepared = new PreparedChunkData[chunksToLoad.Count];
        var useSequentialPrepare = IsStargateBiomeMap(gridUid, out _) && _stargateMapMotion.GetValueOrDefault(gridUid) >= 4f;
        if (useSequentialPrepare)
        {
            for (var i = 0; i < chunksToLoad.Count; i++)
            {
                prepared[i] = PrepareChunkData(component, gridUid, grid, chunksToLoad[i], seed, restricted);
            }
        }
        else
        {
            Parallel.For(0, chunksToLoad.Count,
                new ParallelOptions { MaxDegreeOfParallelism = _parallel.ParallelProcessCount },
                i =>
                {
                    prepared[i] = PrepareChunkData(component, gridUid, grid, chunksToLoad[i], seed, restricted);
                });
        }
        for (var i = 0; i < prepared.Length; i++)
        {
            component.LoadedChunks.Add(chunksToLoad[i]);
            ApplyPreparedChunk(component, gridUid, grid, prepared[i], entityBudget, decalBudget, ref entityBudgetLeft, ref decalBudgetLeft);
        }
    }

    /// <summary>
    /// Goes through all marker chunks that haven't been calculated, then calculates what spawns there are and
    /// allocates them to the relevant actual chunks in the biome (marker chunks may be many times larger than biome chunks).
    /// </summary>
    private void BuildMarkerChunks(BiomeComponent component, EntityUid gridUid, MapGridComponent grid, int seed)
    {
        var markers = _markerChunks[component];
        var loadedMarkers = component.LoadedMarkers;
        var isStargate = IsStargateBiomeMap(gridUid, out var destination);
        var motion = isStargate ? _stargateMapMotion.GetValueOrDefault(gridUid) : 0f;
        var pendingMarkersTotal = isStargate ? CountPendingMarkers(component) : 0;
        var pendingDynamicSpawns = isStargate ? CountPendingDynamicSpawns(component) : 0;
        var markerChunkBudget = _markerChunkBudget;
        var hardPause = false;

        if (!isStargate)
            _stargateHardPauseMaps.Remove(gridUid);

        if (isStargate)
        {
            var hardPauseLatched = _stargateHardPauseMaps.Contains(gridUid);
            var shouldEnterHardPause = pendingMarkersTotal >= 1750 ||
                                       pendingDynamicSpawns >= 700 ||
                                       (motion >= 0.90f && pendingMarkersTotal >= 800);
            var canExitHardPause = pendingMarkersTotal <= 1300 &&
                                   pendingDynamicSpawns <= 250 &&
                                   motion <= 0.30f;

            if (!hardPauseLatched && shouldEnterHardPause)
                _stargateHardPauseMaps.Add(gridUid);
            else if (hardPauseLatched && canExitHardPause)
                _stargateHardPauseMaps.Remove(gridUid);

            hardPause = _stargateHardPauseMaps.Contains(gridUid);
            if (hardPause)
            {
                markerChunkBudget = 0;
            }

            if (pendingMarkersTotal >= 2400)
                markerChunkBudget = 0;
            else if (pendingMarkersTotal >= 1600)
                markerChunkBudget = Math.Min(markerChunkBudget, 1);
            else if (pendingMarkersTotal >= 900)
                markerChunkBudget = Math.Min(markerChunkBudget, Math.Max(1, markerChunkBudget / 2));

            if (motion >= 1f)
                markerChunkBudget = Math.Min(markerChunkBudget, 1);

            if (destination?.ProgressiveLoadingActive == true)
                markerChunkBudget = Math.Min(markerChunkBudget, 1);
        }

        if (hardPause)
            return;

        var idx = 0;
        var newChunksLeft = markerChunkBudget;

        foreach (var (layer, chunks) in markers)
        {
            idx++;
            var localIdx = idx;

            const double MarkerRespawnChance = 0.35;
            var respawnEligible = component.RespawnEligibleMarkers;
            var toProcess = new List<Vector2i>();
            foreach (var chunk in chunks)
            {
                if (loadedMarkers.TryGetValue(layer, out var alreadyLoaded) && alreadyLoaded.Contains(chunk))
                    continue;

                if (markerChunkBudget > 0 && newChunksLeft <= 0)
                    continue;

                toProcess.Add(chunk);
                newChunksLeft--;
            }

            if (toProcess.Count == 0)
                continue;

            var useParallel = !(isStargate && (pendingMarkersTotal >= 600 || motion >= 0.75f || destination?.ProgressiveLoadingActive == true));
            void ProcessChunk(Vector2i chunk)
            {
                bool isRespawnEligible;
                lock (loadedMarkers)
                {
                    isRespawnEligible = respawnEligible.TryGetValue(layer, out var eligible) && eligible.Remove(chunk);
                }
                if (isRespawnEligible)
                {
                    if (_random.NextDouble() >= MarkerRespawnChance)
                    {
                        lock (loadedMarkers)
                        {
                            if (!loadedMarkers.TryGetValue(layer, out var lockMobChunks))
                            {
                                lockMobChunks = new HashSet<Vector2i>();
                                loadedMarkers[layer] = lockMobChunks;
                            }
                            lockMobChunks.Add(chunk);
                        }
                        return;
                    }
                }

                var forced = component.ForcedMarkerLayers.Contains(layer);

                var pending = new Dictionary<Vector2i, Dictionary<string, List<Vector2i>>>();

                var layerProto = ProtoManager.Index<BiomeMarkerLayerPrototype>(layer);
                var markerSeed = seed + chunk.X * ChunkSize + chunk.Y + localIdx;
                var rand = new Random(markerSeed);
                if (rand.NextDouble() > layerProto.SpawnChance)
                    return;
                var buffer = (int)(layerProto.Radius / 2f);
                var bounds = new Box2i(chunk + buffer, chunk + layerProto.Size - buffer);
                var count = (int)(bounds.Area / (layerProto.Radius * layerProto.Radius));
                count = Math.Min(count, layerProto.MaxCount);

                GetMarkerNodes(gridUid, component, grid, layerProto, forced, bounds, count, rand,
                    out var spawnSet, out var existing);

                if (forced && existing.Count > 0)
                {
                    lock (component.PendingMarkers)
                    {
                        foreach (var ent in existing)
                        {
                            Del(ent);
                        }
                    }
                }

                foreach (var node in spawnSet.Keys)
                {
                    var chunkOrigin = SharedMapSystem.GetChunkIndices(node, ChunkSize) * ChunkSize;

                    if (!pending.TryGetValue(chunkOrigin, out var pendingMarkers))
                    {
                        pendingMarkers = new Dictionary<string, List<Vector2i>>();
                        pending[chunkOrigin] = pendingMarkers;
                    }

                    if (!pendingMarkers.TryGetValue(layer, out var layerMarkers))
                    {
                        layerMarkers = new List<Vector2i>();
                        pendingMarkers[layer] = layerMarkers;
                    }

                    layerMarkers.Add(node);
                }

                lock (loadedMarkers)
                {
                    if (!loadedMarkers.TryGetValue(layer, out var lockMobChunks))
                    {
                        lockMobChunks = new HashSet<Vector2i>();
                        loadedMarkers[layer] = lockMobChunks;
                    }

                    lockMobChunks.Add(chunk);

                    foreach (var (chunkOrigin, layers) in pending)
                    {
                        if (!component.PendingMarkers.TryGetValue(chunkOrigin, out var lockMarkers))
                        {
                            lockMarkers = new Dictionary<string, List<Vector2i>>();
                            component.PendingMarkers[chunkOrigin] = lockMarkers;
                        }

                        foreach (var (lockLayer, nodes) in layers)
                        {
                            lockMarkers[lockLayer] = nodes;
                        }
                    }
                }
            }

            if (useParallel)
            {
                Parallel.ForEach(toProcess, new ParallelOptions() { MaxDegreeOfParallelism = _parallel.ParallelProcessCount }, ProcessChunk);
            }
            else
            {
                foreach (var chunk in toProcess)
                {
                    ProcessChunk(chunk);
                }
            }
        }

        component.ForcedMarkerLayers.Clear();
    }

    /// <summary>
    /// Gets the marker nodes for the specified area.
    /// </summary>
    /// <param name="emptyTiles">Should we include empty tiles when determine markers (e.g. if they are yet to be loaded)</param>
    public void GetMarkerNodes(
        EntityUid gridUid,
        BiomeComponent biome,
        MapGridComponent grid,
        BiomeMarkerLayerPrototype layerProto,
        bool forced,
        Box2i bounds,
        int count,
        Random rand,
        out Dictionary<Vector2i, string?> spawnSet,
        out HashSet<EntityUid> existingEnts,
        bool emptyTiles = true)
    {
        DebugTools.Assert(count > 0);
        var remainingTiles = _tilePool.Get();
        var nodeEntities = new Dictionary<Vector2i, EntityUid?>();
        var nodeMask = new Dictionary<Vector2i, string?>();

        var hasRestriction = false; // Lua start
        var origin = Vector2.Zero;
        var range2 = 0f;
        if (TryComp<RestrictedRangeComponent>(gridUid, out var restricted))
        {
            hasRestriction = true;
            origin = restricted.Origin;
            range2 = restricted.Range * restricted.Range;
        } // Lua end

        const int StargateSafeRadiusTiles = 18;
        const int StargateSafeRadiusSq = StargateSafeRadiusTiles * StargateSafeRadiusTiles;
        var inStargateSafeZone = TryComp<StargateDestinationComponent>(gridUid, out var stargateDest);

        // Okay so originally we picked a random tile and BFS outwards
        // the problem is if you somehow get a cooked frontier then it might drop entire veins
        // hence we'll grab all valid tiles up front and use that as possible seeds.
        // It's hella more expensive but stops issues.
        for (var x = bounds.Left; x < bounds.Right; x++)
        {
            for (var y = bounds.Bottom; y < bounds.Top; y++)
            {
                var node = new Vector2i(x, y);

                if (hasRestriction) // Lua start
                {
                    var dx = node.X - origin.X;
                    var dy = node.Y - origin.Y;
                    if ((dx * dx + dy * dy) > range2) continue;
                } // Lua end
                if (inStargateSafeZone && stargateDest != null)
                {
                    var sx = node.X - stargateDest.Origin.X;
                    var sy = node.Y - stargateDest.Origin.Y;
                    if (sx * sx + sy * sy <= StargateSafeRadiusSq)
                        continue;
                }

                // Empty tile, skip if relevant.
                if (!emptyTiles && (!_mapSystem.TryGetTile(grid, node, out var tile) || tile.IsEmpty))
                    continue;

                // Check if it's a valid spawn, if so then use it.
                var enumerator = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, node);
                enumerator.MoveNext(out var existing);

                if (!forced && existing != null)
                    continue;

                // Check if mask matches // anything blocking.
                TryGetEntity(node, biome, (gridUid, grid), out var proto);

                // If there's an existing entity and it doesn't match the mask then skip.
                if (layerProto.EntityMask.Count > 0 &&
                    (proto == null ||
                     !layerProto.EntityMask.ContainsKey(proto)))
                {
                    continue;
                }

                // If it's just a flat spawn then just check for anything blocking.
                if (proto != null && layerProto.Prototype != null)
                {
                    continue;
                }

                DebugTools.Assert(layerProto.EntityMask.Count == 0 || !string.IsNullOrEmpty(proto));
                remainingTiles.Add(node);
                nodeEntities.Add(node, existing);
                nodeMask.Add(node, proto);
            }
        }

        var frontier = new ValueList<Vector2i>(32);
        var frontierSet = new HashSet<Vector2i>(32);
        // TODO: Need poisson but crashes whenever I use moony's due to inputs or smth idk
        // Get the total amount of groups to spawn across the entire chunk.
        // We treat a null entity mask as requiring nothing else on the tile

        spawnSet = new Dictionary<Vector2i, string?>();
        existingEnts = new HashSet<EntityUid>();

        // Iterate the group counts and pathfind out each group.
        for (var i = 0; i < count; i++)
        {
            var groupSize = rand.Next(layerProto.MinGroupSize, layerProto.MaxGroupSize + 1);

            // While we have remaining tiles keep iterating
            while (groupSize > 0 && remainingTiles.Count > 0)
            {
                var startNode = rand.PickAndTake(remainingTiles);
                frontier.Clear();
                frontierSet.Clear();
                frontier.Add(startNode);
                frontierSet.Add(startNode);

                // This essentially may lead to a vein being split in multiple areas but the count matters more than position.
                while (frontier.Count > 0 && groupSize > 0)
                {
                    // Need to pick a random index so we don't just get straight lines of ores.
                    var frontierIndex = rand.Next(frontier.Count);
                    var node = frontier[frontierIndex];
                    frontier.RemoveSwap(frontierIndex);
                    frontierSet.Remove(node);
                    remainingTiles.Remove(node);

                    // Add neighbors if they're valid, worst case we add no more and pick another random seed tile.
                    for (var x = -1; x <= 1; x++)
                    {
                        for (var y = -1; y <= 1; y++)
                        {
                            var neighbor = new Vector2i(node.X + x, node.Y + y);

                            if (frontierSet.Contains(neighbor) || !remainingTiles.Contains(neighbor))
                                continue;

                            frontier.Add(neighbor);
                            frontierSet.Add(neighbor);
                        }
                    }

                    // Tile valid salad so add it.
                    var mask = nodeMask[node];
                    spawnSet.Add(node, mask);
                    groupSize--;
                }
            }

            if (groupSize > 0)
            {
                var now = Environment.TickCount64;
                if (now >= _nextOreVeinWarningTickMs)
                {
                    Log.Warning($"Found remaining group size for ore veins!");
                    _nextOreVeinWarningTickMs = now + OreVeinWarningIntervalMs;
                }
            }
        }

        _tilePool.Return(remainingTiles);
    }

    /// <summary>
    /// Loads the pre-deteremined marker nodes for a particular chunk.
    /// This is calculated in <see cref="BuildMarkerChunks"/>
    /// </summary>
    /// <remarks>
    /// Note that the marker chunks do not correspond to this chunk.
    /// </remarks>
    private int LoadChunkMarkers(
        BiomeComponent component,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i chunk,
        int seed,
        RestrictedRangeComponent? restricted = null,
        int budgetLeft = int.MaxValue)
    {
        if (!component.PendingMarkers.TryGetValue(chunk, out var layers))
            return budgetLeft;

        component.ModifiedTiles.TryGetValue(chunk, out var modified);
        modified ??= _tilePool.Get();

        var exhausted = false;
        var spawnedAny = false;
        var remainingLayers = new Dictionary<string, List<Vector2i>>();

        foreach (var (layer, nodes) in layers)
        {
            var layerProto = ProtoManager.Index<BiomeMarkerLayerPrototype>(layer);

            var hasRestriction = restricted != null; // Lua
            var origin = restricted?.Origin ?? Vector2.Zero;
            var range2 = restricted != null ? restricted.Range * restricted.Range : 0f;
            const int StargateSafeRadiusTiles = 18;
            const int StargateSafeRadiusSq = StargateSafeRadiusTiles * StargateSafeRadiusTiles;
            var inStargateSafeZone = TryComp<StargateDestinationComponent>(gridUid, out var stargateDest);

            List<Vector2i>? deferred = null;

            foreach (var node in nodes)
            {
                if (_markerBudget > 0 && budgetLeft <= 0)
                {
                    deferred ??= new List<Vector2i>();
                    deferred.Add(node);
                    exhausted = true;
                    continue;
                }

                if (hasRestriction) // Lua start
                {
                    var dx = node.X - origin.X;
                    var dy = node.Y - origin.Y;
                    if ((dx * dx + dy * dy) > range2) continue;
                } // Lua end
                if (inStargateSafeZone && stargateDest != null)
                {
                    var sx = node.X - stargateDest.Origin.X;
                    var sy = node.Y - stargateDest.Origin.Y;
                    if (sx * sx + sy * sy <= StargateSafeRadiusSq)
                        continue;
                }
                if (modified.Contains(node))
                    continue;

                if (TryGetBiomeTile(node, component.Layers, seed, (gridUid, grid), out var tile))
                {
                    _mapSystem.SetTile(gridUid, grid, node, tile.Value);
                }

                if (!TryResolveMarkerSpawn(node, component, (gridUid, grid), layerProto, out var prototype, out var replacements))
                    continue;

                foreach (var replacement in replacements)
                {
                    Del(replacement);
                }

                var uid = EntityManager.CreateEntityUninitialized(prototype, _mapSystem.GridTileToLocal(gridUid, grid, node));
                RemComp<GhostTakeoverAvailableComponent>(uid);
                RemComp<GhostRoleComponent>(uid);
                EntityManager.InitializeAndStartEntity(uid);
                modified.Add(node);
                spawnedAny = true;
                budgetLeft--;

                var layerSize = layerProto.Size;
                var markerChunkOrigin = new Vector2i(chunk.X / layerSize * layerSize, chunk.Y / layerSize * layerSize);
                if (!component.LoadedMarkerEntities.TryGetValue(layer, out var perChunk))
                {
                    perChunk = new Dictionary<Vector2i, List<EntityUid>>();
                    component.LoadedMarkerEntities[layer] = perChunk;
                }
                if (!perChunk.TryGetValue(markerChunkOrigin, out var list))
                {
                    list = new List<EntityUid>();
                    perChunk[markerChunkOrigin] = list;
                }
                list.Add(uid);
            }

            if (deferred is { Count: > 0 })
                remainingLayers[layer] = deferred;
        }

        if (!spawnedAny && modified.Count == 0)
        {
            _tilePool.Return(modified);
        }
        else
        {
            component.ModifiedTiles[chunk] = modified;
        }

        if (exhausted && remainingLayers.Count > 0)
        {
            component.PendingMarkers[chunk] = remainingLayers;
        }
        else
        {
            component.PendingMarkers.Remove(chunk);
        }

        return budgetLeft;
    }

    private bool TryResolveMarkerSpawn(Vector2i node, BiomeComponent component, Entity<MapGridComponent> grid, BiomeMarkerLayerPrototype layerProto, [NotNullWhen(true)] out string? prototype, out List<EntityUid> replacements)
    {
        replacements = new List<EntityUid>();
        if (layerProto.EntityMask.Count == 0)
        {
            prototype = layerProto.Prototype;
            return prototype != null;
        }
        string? maskedPrototype = null;
        var matchedBiomeMask = false;
        if (TryGetEntity(node, component, grid, out var biomePrototype) && layerProto.EntityMask.TryGetValue(biomePrototype, out var biomeMaskedPrototype))
        {
            maskedPrototype = biomeMaskedPrototype;
            matchedBiomeMask = true;
        }

        var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(grid.Owner, grid.Comp, node);
        while (anchored.MoveNext(out var ent))
        {
            if (!TryComp<MetaDataComponent>(ent.Value, out var meta) || meta.EntityPrototype?.ID is not { } existingProto || !layerProto.EntityMask.TryGetValue(existingProto, out var nextPrototype))
            { continue; }
            maskedPrototype ??= nextPrototype;
            if (maskedPrototype != nextPrototype) continue;
            replacements.Add(ent.Value);
        }
        prototype = maskedPrototype;
        if (prototype == null) return false;
        return matchedBiomeMask || replacements.Count > 0;
    }

    /// <summary>
    /// Loads a particular queued chunk for a biome.
    /// </summary>
    private PreparedChunkData PrepareChunkData(
        BiomeComponent component,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i chunk,
        int seed,
        RestrictedRangeComponent? restricted)
    {
        var data = new PreparedChunkData { Chunk = chunk };
        component.ModifiedTiles.TryGetValue(chunk, out var modified);

        var hasRestriction = restricted != null; // Lua
        var origin = restricted?.Origin ?? Vector2.Zero;
        var range2 = restricted != null ? restricted.Range * restricted.Range : 0f;
        for (var x = 0; x < ChunkSize; x++)
        {
            for (var y = 0; y < ChunkSize; y++)
            {
                var indices = new Vector2i(x + chunk.X, y + chunk.Y);

                if (hasRestriction) // Lua
                {
                    var dx = indices.X - origin.X;
                    var dy = indices.Y - origin.Y;
                    if ((dx * dx + dy * dy) > range2) continue;
                }

                if (modified != null && modified.Contains(indices))
                    continue;

                if (_mapSystem.TryGetTileRef(gridUid, grid, indices, out var tileRef) && !tileRef.Tile.IsEmpty)
                    continue;

                if (!TryGetTile(indices, component.Layers, seed, (gridUid, grid), out var biomeTile))
                    continue;

                data.Tiles.Add((indices, biomeTile.Value));
            }
        }

        for (var x = 0; x < ChunkSize; x++)
        {
            for (var y = 0; y < ChunkSize; y++)
            {
                var indices = new Vector2i(x + chunk.X, y + chunk.Y);

                if (modified != null && modified.Contains(indices))
                    continue;

                if (hasRestriction) // Lua
                {
                    var dx = indices.X - origin.X;
                    var dy = indices.Y - origin.Y;
                    if ((dx * dx + dy * dy) > range2) continue;
                }

                var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, indices);
                if (anchored.MoveNext(out _) || !TryGetEntity(indices, component, (gridUid, grid), out var entPrototype, out var spacing))
                    continue;

                if (spacing > 0 && HasNearbyAnchoredEntity(gridUid, grid, indices, spacing))
                    continue;

                data.Entities.Add((indices, entPrototype));
            }
        }

        // Decals
        for (var x = 0; x < ChunkSize; x++)
        {
            for (var y = 0; y < ChunkSize; y++)
            {
                var indices = new Vector2i(x + chunk.X, y + chunk.Y);

                if (modified != null && modified.Contains(indices))
                    continue;

                if (hasRestriction) // Lua
                {
                    var dx = indices.X - origin.X;
                    var dy = indices.Y - origin.Y;
                    if ((dx * dx + dy * dy) > range2) continue;
                }

                var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, indices);
                if (anchored.MoveNext(out _) || !TryGetDecals(indices, component.Layers, seed, (gridUid, grid), out var decals))
                    continue;

                foreach (var decal in decals)
                {
                    data.Decals.Add((indices, decal.ID, decal.Position));
                }
            }
        }

        return data;
    }

    private void ApplyPreparedChunk(
        BiomeComponent component,
        EntityUid gridUid,
        MapGridComponent grid,
        PreparedChunkData data,
        int entityBudget,
        int decalBudget,
        ref int entityBudgetLeft,
        ref int decalBudgetLeft)
    {
        _mapSystem.SetTiles(gridUid, grid, data.Tiles);
        var loadedEntities = new Dictionary<EntityUid, Vector2i>();
        component.LoadedEntities.Add(data.Chunk, loadedEntities);

        var pendingEnts = new List<(Vector2i indices, string prototype)>();
        foreach (var (indices, prototype) in data.Entities)
        {
            if (entityBudget > 0 && entityBudgetLeft <= 0)
            {
                pendingEnts.Add((indices, prototype));
                continue;
            }

            var ent = Spawn(prototype, _mapSystem.GridTileToLocal(gridUid, grid, indices));

            if (_xformQuery.TryGetComponent(ent, out var xform) && !xform.Anchored)
                _transform.AnchorEntity((ent, xform), (gridUid, grid), indices);

            loadedEntities.Add(ent, indices);
            entityBudgetLeft--;
        }
        if (pendingEnts.Count > 0)
            component.PendingEntities[data.Chunk] = pendingEnts;

        var loadedDecals = new Dictionary<uint, Vector2i>();
        component.LoadedDecals.Add(data.Chunk, loadedDecals);

        var pendingForChunk = new List<(Vector2i indices, string decalId, Vector2 position)>();
        foreach (var (indices, decalId, position) in data.Decals)
        {
            if (decalBudget > 0 && decalBudgetLeft <= 0)
            {
                pendingForChunk.Add((indices, decalId, position));
                continue;
            }
            if (!_decals.TryAddDecal(decalId, new EntityCoordinates(gridUid, position), out var dec))
                continue;

            loadedDecals.Add(dec, indices);
            decalBudgetLeft--;
        }
        if (pendingForChunk.Count > 0)
            component.PendingDecals[data.Chunk] = pendingForChunk;
    }

    private bool HasNearbyAnchoredEntity(EntityUid gridUid, MapGridComponent grid, Vector2i indices, int spacing)
    {
        for (var dx = -spacing; dx <= spacing; dx++)
        {
            for (var dy = -spacing; dy <= spacing; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                var neighbor = new Vector2i(indices.X + dx, indices.Y + dy);
                var enumerator = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, neighbor);

                if (enumerator.MoveNext(out _))
                    return true;
            }
        }

        return false;
    }

    private void ProcessMarkerChunkUnloads(BiomeComponent component)
    {
        if (!_markerChunks.TryGetValue(component, out var markers))
            return;

        foreach (var (layer, loadedChunks) in component.LoadedMarkers)
        {
            if (!markers.TryGetValue(layer, out var inRangeChunks))
                inRangeChunks = new HashSet<Vector2i>();

            foreach (var chunk in loadedChunks.ToList())
            {
                if (inRangeChunks.Contains(chunk))
                    continue;

                if (!component.LoadedMarkerEntities.TryGetValue(layer, out var perChunk) ||
                    !perChunk.TryGetValue(chunk, out var entities))
                {
                    loadedChunks.Remove(chunk);
                    continue;
                }

                var allGone = true;
                foreach (var uid in entities)
                {
                    if (!Deleted(uid))
                    {
                        allGone = false;
                        break;
                    }
                }

                if (!allGone)
                    continue;

                loadedChunks.Remove(chunk);
                perChunk.Remove(chunk);
                if (perChunk.Count == 0)
                    component.LoadedMarkerEntities.Remove(layer);

                if (!component.RespawnEligibleMarkers.TryGetValue(layer, out var eligible))
                {
                    eligible = new HashSet<Vector2i>();
                    component.RespawnEligibleMarkers[layer] = eligible;
                }
                eligible.Add(chunk);
            }
        }
    }

    #endregion

    #region Unload

    /// <summary>
    /// Handles all of the queued chunk unloads for a particular biome.
    /// </summary>
    private void UnloadChunks(BiomeComponent component, EntityUid gridUid, MapGridComponent grid, int seed, int chunkBudget)
    {
        var active = _activeChunks[component];
        _unloadChunksBuffer.Clear();

        foreach (var chunk in component.LoadedChunks)
        {
            if (!active.Contains(chunk))
                _unloadChunksBuffer.Add(chunk);
        }

        if (_unloadChunksBuffer.Count == 0)
            return;

        var unloaded = 0;
        foreach (var chunk in _unloadChunksBuffer)
        {
            UnloadChunk(component, gridUid, grid, chunk, seed, _unloadTilesBuffer);
            unloaded++;

            if (unloaded >= chunkBudget)
                break;
        }
    }

    /// <summary>
    /// Unloads a specific biome chunk.
    /// </summary>
    private void UnloadChunk(BiomeComponent component, EntityUid gridUid, MapGridComponent grid, Vector2i chunk, int seed, List<(Vector2i, Tile)> tiles)
    {
        component.PendingEntities.Remove(chunk);
        component.PendingDecals.Remove(chunk);

        // Reverse order to loading
        component.ModifiedTiles.TryGetValue(chunk, out var modified);
        modified ??= _tilePool.Get();

        // Delete decals
        foreach (var (dec, indices) in component.LoadedDecals[chunk])
        {
            // If we couldn't remove it then flag the tile to never be touched.
            if (!_decals.RemoveDecal(gridUid, dec))
            {
                modified.Add(indices);
            }
        }

        component.LoadedDecals.Remove(chunk);

        // Delete entities
        // Ideally any entities that aren't modified just get deleted and re-generated later
        // This is because if we want to save the map (e.g. persistent server) it makes the file much smaller
        // and also if the map is enormous will make stuff like physics broadphase much faster
        var xformQuery = GetEntityQuery<TransformComponent>();

        foreach (var (ent, tile) in component.LoadedEntities[chunk])
        {
            if (Deleted(ent) || !xformQuery.TryGetComponent(ent, out var xform))
            {
                modified.Add(tile);
                continue;
            }

            // It's moved
            var entTile = _mapSystem.LocalToTile(gridUid, grid, xform.Coordinates);

            if (!xform.Anchored || entTile != tile)
            {
                modified.Add(tile);
                continue;
            }

            if (!EntityManager.IsDefault(ent, BiomeUnloadIgnoredComponents))
            {
                modified.Add(tile);
                continue;
            }

            Del(ent);
        }

        component.LoadedEntities.Remove(chunk);

        // Unset tiles (if the data is custom)

        for (var x = 0; x < ChunkSize; x++)
        {
            for (var y = 0; y < ChunkSize; y++)
            {
                var indices = new Vector2i(x + chunk.X, y + chunk.Y);

                if (modified.Contains(indices))
                    continue;

                // Don't mess with anything that's potentially anchored.
                var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, indices);

                if (anchored.MoveNext(out _))
                {
                    modified.Add(indices);
                    continue;
                }

                // If it's default data unload the tile.
                if (!TryGetBiomeTile(indices, component.Layers, seed, null, out var biomeTile) ||
                    _mapSystem.TryGetTileRef(gridUid, grid, indices, out var tileRef) && tileRef.Tile != biomeTile.Value)
                {
                    modified.Add(indices);
                    continue;
                }

                tiles.Add((indices, Tile.Empty));
            }
        }

        _mapSystem.SetTiles(gridUid, grid, tiles);
        tiles.Clear();
        component.LoadedChunks.Remove(chunk);

        if (modified.Count == 0)
        {
            if (component.ModifiedTiles.Remove(chunk, out var toReturn))
            {
                toReturn.Clear();
                _tilePool.Return(toReturn);
            }
            else
            {
                modified.Clear();
                _tilePool.Return(modified);
            }
        }
        else
        {
            component.ModifiedTiles[chunk] = modified;
        }
    }

    #endregion

    /// <summary>
    /// Creates a simple planet setup for a map.
    /// </summary>
    public void EnsurePlanet(EntityUid mapUid, BiomeTemplatePrototype biomeTemplate, int? seed = null, MetaDataComponent? metadata = null, Color? mapLight = null)
    {
        if (!Resolve(mapUid, ref metadata))
            return;

        EnsureComp<MapGridComponent>(mapUid);
        var biome = EntityManager.ComponentFactory.GetComponent<BiomeComponent>();
        seed ??= _random.Next();
        SetSeed(mapUid, biome, seed.Value, false);
        SetTemplate(mapUid, biome, biomeTemplate, false);
        AddComp(mapUid, biome, true);
        Dirty(mapUid, biome, metadata);

        var gravity = EnsureComp<GravityComponent>(mapUid);
        gravity.Enabled = true;
        gravity.Inherent = true;
        Dirty(mapUid, gravity, metadata);

        // Day lighting
        // Daylight: #D8B059
        // Midday: #E6CB8B
        // Moonlight: #2b3143
        // Lava: #A34931
        var light = EnsureComp<MapLightComponent>(mapUid);
        light.AmbientLightColor = mapLight ?? Color.FromHex("#D8B059");
        Dirty(mapUid, light, metadata);

        EnsureComp<RoofComponent>(mapUid);

        EnsureComp<LightCycleComponent>(mapUid);

        EnsureComp<SunShadowComponent>(mapUid);
        EnsureComp<SunShadowCycleComponent>(mapUid);
        EnsureComp<MapperGridComponent>(mapUid);

        var moles = new float[Atmospherics.AdjustedNumberOfGases];
        moles[(int)Gas.Oxygen] = 21.824779f;
        moles[(int)Gas.Nitrogen] = 82.10312f;

        var mixture = new GasMixture(moles, Atmospherics.T20C);

        _atmos.SetMapAtmosphere(mapUid, false, mixture);
    }

    /// <summary>
    /// Sets the specified tiles as relevant and marks them as modified.
    /// </summary>
    public void ReserveTiles(EntityUid mapUid, Box2 bounds, List<(Vector2i Index, Tile Tile)> tiles, BiomeComponent? biome = null, MapGridComponent? mapGrid = null)
    {
        if (!Resolve(mapUid, ref biome, ref mapGrid, false))
            return;

        foreach (var tileSet in _mapSystem.GetLocalTilesIntersecting(mapUid, mapGrid, bounds, false))
        {
            Vector2i chunkOrigin;
            HashSet<Vector2i> modified;

            // Existing, ignore
            if (_mapSystem.TryGetTileRef(mapUid, mapGrid, tileSet.GridIndices, out var existingRef) && !existingRef.Tile.IsEmpty)
            {
                chunkOrigin = SharedMapSystem.GetChunkIndices(tileSet.GridIndices, ChunkSize) * ChunkSize;
                modified = biome.ModifiedTiles.GetOrNew(chunkOrigin);
                modified.Add(tileSet.GridIndices);
                continue;
            }

            if (!TryGetBiomeTile(tileSet.GridIndices, biome.Layers, biome.Seed, (mapUid, mapGrid), out var tile))
            {
                continue;
            }

            chunkOrigin = SharedMapSystem.GetChunkIndices(tileSet.GridIndices, ChunkSize) * ChunkSize;
            modified = biome.ModifiedTiles.GetOrNew(chunkOrigin);
            modified.Add(tileSet.GridIndices);
            tiles.Add((tileSet.GridIndices, tile.Value));
        }

        _mapSystem.SetTiles(mapUid, mapGrid, tiles);
    }
}
