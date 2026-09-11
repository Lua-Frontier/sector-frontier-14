using System.Linq;
using System.Numerics;
using Content.Server._Mono.Cleanup;
using Content.Server.Worldgen.Components;
using Content.Server.Worldgen.Components.Debris;
using Content.Server.Worldgen.Systems.GC;
using Content.Server.Worldgen.Tools;
using Content.Server._Lua.Stargate.Components;
using Content.Shared.CCVar;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using Content.Server.Shuttles.Components;
using Content.Server._NF.Worldgen.Components.Debris; // Frontier
using Content.Server._Lua.Shuttles.Systems;

namespace Content.Server.Worldgen.Systems.Debris;

/// <summary>
///     This handles placing debris within the world evenly with rng, primarily for structures like asteroid fields.
/// </summary>
public sealed class DebrisFeaturePlacerSystem : BaseWorldSystem
{
    [Dependency] private readonly GCQueueSystem _gc = default!;
    [Dependency] private readonly NoiseIndexSystem _noiseIndex = default!;
    [Dependency] private readonly PoissonDiskSampler _sampler = default!;
    [Dependency] private readonly TransformSystem _xformSys = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _protos = default!;
    [Dependency] private readonly ShuttleGridAccessSystem _gridAccess = default!;

    private ISawmill _sawmill = default!;
    private const int StargateInitialDebrisBudget = 18;
    private const int StargateTickDebrisBudget = 12;

    private List<Entity<MapGridComponent>> _mapGrids = new();
    private readonly HashSet<EntityUid> _pendingPlacers = new();
    private readonly List<EntityUid> _pendingPlacerScratch = new();
    private bool _debrisPregenEnabled;
    private float _debrisPregenRadius;
    private bool _clusterEnabled;
    private float _clusterSpacing;
    private float _clusterRadius;
    private float _clusterJitter;
    private float _clusterCountScale;
    private string _mapGridCompName = "MapGrid";

    /// <inheritdoc />
    public override void Initialize()
    {
        _sawmill = _logManager.GetSawmill("world.debris.feature_placer");
        _mapGridCompName = Factory.GetComponentName<MapGridComponent>();
        SubscribeLocalEvent<DebrisFeaturePlacerControllerComponent, WorldChunkLoadedEvent>(OnChunkLoaded);
        SubscribeLocalEvent<DebrisFeaturePlacerControllerComponent, WorldChunkUnloadedEvent>(OnChunkUnloaded);
        SubscribeLocalEvent<DebrisFeaturePlacerControllerComponent, ComponentShutdown>(OnPlacerShutdown);
        SubscribeLocalEvent<OwnedDebrisComponent, ComponentShutdown>(OnDebrisShutdown);
        SubscribeLocalEvent<OwnedDebrisComponent, MoveEvent>(OnDebrisMove);
        SubscribeLocalEvent<OwnedDebrisComponent, TryCancelGC>(OnTryCancelGC); // Mono Re-add
        SubscribeLocalEvent<SimpleDebrisSelectorComponent, TryGetPlaceableDebrisFeatureEvent>(
            OnTryGetPlacableDebrisEvent);
        Subs.CVar(_cfg, CCVars.WorldgenDebrisPregenEnabled, value => _debrisPregenEnabled = value, true);
        Subs.CVar(_cfg, CCVars.WorldgenDebrisPregenRadius, value => _debrisPregenRadius = value, true);
        Subs.CVar(_cfg, CCVars.WorldgenDebrisClusterEnabled, value => _clusterEnabled = value, true);
        Subs.CVar(_cfg, CCVars.WorldgenDebrisClusterSpacing, value => _clusterSpacing = value, true);
        Subs.CVar(_cfg, CCVars.WorldgenDebrisClusterRadius, value => _clusterRadius = value, true);
        Subs.CVar(_cfg, CCVars.WorldgenDebrisClusterJitter, value => _clusterJitter = value, true);
        Subs.CVar(_cfg, CCVars.WorldgenDebrisClusterCountScale, value => _clusterCountScale = value, true);
    }

    public override void Update(float frameTime)
    {
        if (_pendingPlacers.Count == 0)
            return;

        _pendingPlacerScratch.Clear();
        _pendingPlacerScratch.AddRange(_pendingPlacers);

        foreach (var uid in _pendingPlacerScratch)
        {
            if (!TryComp<DebrisFeaturePlacerControllerComponent>(uid, out var comp) ||
                comp.PendingPoints == null ||
                comp.PendingChunk == null)
            {
                _pendingPlacers.Remove(uid);
                continue;
            }

            if (!TryComp<WorldChunkComponent>(comp.PendingChunk.Value, out var chunk))
            {
                ClearPending(uid, comp);
                continue;
            }

            if (!HasComp<LoadedChunkComponent>(comp.PendingChunk.Value))
                continue;

            if (!TryComp<MapComponent>(chunk.Map, out var map))
                continue;

            var done = PlaceDebrisPoints(uid, comp, comp.PendingChunk.Value, chunk.Map, map.MapId, comp.PendingPoints, comp.PendingPointIndex, StargateTickDebrisBudget, false);
            comp.PendingPointIndex = done;

            if (comp.PendingPointIndex >= comp.PendingPoints.Count)
                ClearPending(uid, comp);
        }
    }

    private void OnPlacerShutdown(EntityUid uid, DebrisFeaturePlacerControllerComponent component, ComponentShutdown args)
    {
        _pendingPlacers.Remove(uid);
    }

    private void ClearPending(EntityUid uid, DebrisFeaturePlacerControllerComponent component)
    {
        component.PendingPoints = null;
        component.PendingPointIndex = 0;
        component.PendingChunk = null;
        _pendingPlacers.Remove(uid);
    }

    /// <summary>
    ///     Handles GC cancellation in case the chunk is still loaded. - Mono Note: GC is a Discontinued Wizden Feature, but we still use it. Do not remove randomly!
    /// </summary>
    private void OnTryCancelGC(EntityUid uid, OwnedDebrisComponent component, ref TryCancelGC args)
    {
        args.Cancelled |= HasComp<PregenDebrisComponent>(uid) || HasComp<LoadedChunkComponent>(component.OwningController);
    }

    /// <summary>
    ///     Handles debris moving, and making sure it stays parented to a chunk for loading purposes.
    /// </summary>
    private void OnDebrisMove(EntityUid uid, OwnedDebrisComponent component, ref MoveEvent args)
    {
        if (!HasComp<WorldChunkComponent>(component.OwningController))
            return; // Redundant logic, prolly needs it's own handler for your custom system.

        var placer = Comp<DebrisFeaturePlacerControllerComponent>(component.OwningController);
        var xform = args.Component;
        var ownerXform = Transform(component.OwningController);
        if (xform.MapUid is null || ownerXform.MapUid is null)
            return; // not our problem

        if (xform.MapUid != ownerXform.MapUid)
        {
            _sawmill.Error($"Somehow debris {uid} left it's expected map! Unparenting it to avoid issues.");
            RemCompDeferred<OwnedDebrisComponent>(uid);
            placer.OwnedDebris.Remove(component.LastKey);
            return;
        }

        placer.OwnedDebris.Remove(component.LastKey);
        var newChunk = GetOrCreateChunk(GetChunkCoords(uid), xform.MapUid!.Value);
        if (newChunk is null || !TryComp<DebrisFeaturePlacerControllerComponent>(newChunk, out var newPlacer))
        {
            // Whelp.
            RemCompDeferred<OwnedDebrisComponent>(uid);
            return;
        }

        newPlacer.OwnedDebris[_xformSys.GetWorldPosition(xform)] = uid; // Change our owner.
        component.OwningController = newChunk.Value;
    }

    /// <summary>
    ///     Handles debris shutdown/detach.
    /// </summary>
    private void OnDebrisShutdown(EntityUid uid, OwnedDebrisComponent component, ComponentShutdown args)
    {
        if (!TryComp<DebrisFeaturePlacerControllerComponent>(component.OwningController, out var placer))
            return;

        placer.OwnedDebris[component.LastKey] = null;
        if (Terminating(uid))
            placer.OwnedDebris.Remove(component.LastKey);
    }

    /// <summary>
    ///     Queues all debris owned by the placer for garbage collection.
    /// </summary>
    private void OnChunkUnloaded(EntityUid uid, DebrisFeaturePlacerControllerComponent component,
        ref WorldChunkUnloadedEvent args)
    {
        if (component.Pregenerated)
        {
            foreach (var (_, debris) in component.OwnedDebris)
            {
                if (debris is null || Deleted(debris.Value))
                    continue;

                if (!HasComp<PregenDebrisComponent>(debris.Value))
                {
                    _gc.TryGCEntity(debris.Value);
                    continue;
                }

                DisarmPregenLocality(debris.Value);
            }

            return;
        }

        foreach (var (_, debris) in component.OwnedDebris) // Mono Re-add
        {
            if (debris is not null)
                _gc.TryGCEntity(debris.Value); // gonb.
        }

        component.DoSpawns = true;
        ClearPending(uid, component);
    }

    /// <summary>
    ///     Handles providing a debris type to place for SimpleDebrisSelectorComponent.
    ///     This randomly picks a debris type from the EntitySpawnCollectionCache.
    /// </summary>
    private void OnTryGetPlacableDebrisEvent(EntityUid uid, SimpleDebrisSelectorComponent component,
        ref TryGetPlaceableDebrisFeatureEvent args)
    {
        if (args.DebrisProto is not null)
            return;

        var l = new List<string?>(1);
        component.CachedDebrisTable.GetSpawns(_random, ref l);

        switch (l.Count)
        {
            case 0:
                return;
            case > 1:
                _sawmill.Warning($"Got more than one possible debris type from {uid}. List: {string.Join(", ", l)}");
                break;
        }

        args.DebrisProto = l[0];
    }

    /// <summary>
    ///     Handles loading in debris. This does the following:
    ///     - Checks if the debris is currently supposed to do spawns, if it isn't, aborts immediately.
    ///     - Evaluates the density value to be used for placement, if it's zero, aborts.
    ///     - Generates the points to generate debris at, if and only if they've not been selected already by a prior load.
    ///     - Does the following in a loop over all generated points:
    ///         - Raises an event to check if something else wants to intercept debris placement, if the event is handled,
    ///           continues to the next point without generating anything.
    ///         - Raises an event to get the debris type that should be used for generation.
    ///         - Spawns the given debris at the point, adding it to the placer's index.
    /// </summary>
    private void OnChunkLoaded(EntityUid uid, DebrisFeaturePlacerControllerComponent component,
        ref WorldChunkLoadedEvent args)
    {
        if (component.Pregenerated)
        {
            ArmPregenLocality(component);
            SpawnDeferredDebris(uid, component, args.Chunk);
            return;
        }

        if (_debrisPregenEnabled &&
            TryComp<WorldChunkComponent>(args.Chunk, out var pregenChunk) &&
            IsInsidePregenRadius(pregenChunk.Coordinates))
            return;

        TryPlaceDebrisForChunk(args.Chunk, component, false);
    }

    public int TryPlaceDebrisForChunk(
        EntityUid chunkUid,
        DebrisFeaturePlacerControllerComponent component,
        bool pregen,
        int budget = int.MaxValue)
    {
        if (component.DoSpawns == false)
            return 0;

        component.DoSpawns = false; // Don't repeat yourself if this crashes.

        if (!TryComp<WorldChunkComponent>(chunkUid, out var chunk))
            return 0;

        var chunkMap = chunk.Map;

        if (!TryComp<MapComponent>(chunkMap, out var map))
            return 0;

        var densityChannel = component.DensityNoiseChannel;
        var density = _noiseIndex.Evaluate(chunkUid, densityChannel, chunk.Coordinates + new Vector2(0.5f, 0.5f));
        if (density == 0)
        {
            if (pregen)
                component.Pregenerated = true;

            return 0;
        }

        List<Vector2>? points = null;

        // If we've been loaded before, reuse the same coordinates.
        if (component.OwnedDebris.Count != 0)
        {
            //TODO: Remove LINQ.
            points = component.OwnedDebris
                .Where(x => !Deleted(x.Value))
                .Select(static x => x.Key)
                .ToList();
        }

        points ??= GeneratePointsInChunk(chunkUid, density, chunk.Coordinates, chunkMap);

        var mapId = map.MapId;
        var placementBudget = budget;
        if (!pregen && HasComp<StargateDestinationComponent>(chunkMap))
            placementBudget = StargateInitialDebrisBudget;

        var done = PlaceDebrisPoints(chunkUid, component, chunkUid, chunkMap, mapId, points, 0, placementBudget, pregen);

        if (!pregen && done < points.Count)
        {
            component.PendingPoints = points;
            component.PendingPointIndex = done;
            component.PendingChunk = chunkUid;
            _pendingPlacers.Add(chunkUid);
        }

        if (pregen)
        {
            component.Pregenerated = true;
            if (HasComp<LoadedChunkComponent>(chunkUid))
            {
                ArmPregenLocality(component);
                SpawnDeferredDebris(chunkUid, component, chunkUid);
            }
        }

        return done;
    }

    /// <summary>
    /// Checks to see if the potential spawn point is clear
    /// </summary>
    /// <param name="mapId"></param>
    /// <param name="point"></param>
    /// <returns></returns>
    private bool HasCollisions(MapId mapId, Box2 point)
    {
        _mapGrids.Clear();
        _mapManager.FindGridsIntersecting(mapId, point, ref _mapGrids);
        return _mapGrids.Count > 0;
    }

    /// <summary>
    ///     Generates the points to put into a chunk using a poisson disk sampler.
    /// </summary>
    private List<Vector2> GeneratePointsInChunk(EntityUid chunk, float density, Vector2 coords, EntityUid map)
    {
        var offs = (int)((WorldGen.ChunkSize - WorldGen.ChunkSize / 8.0f) / 2.0f);
        var topLeft = new Vector2(-offs, -offs);
        var lowerRight = new Vector2(offs, offs);
        var sampleDistance = density;
        if (_clusterEnabled && _clusterCountScale > 0f)
            sampleDistance /= MathF.Sqrt(_clusterCountScale);

        var enumerator = _sampler.SampleRectangle(topLeft, lowerRight, sampleDistance);
        var debrisPoints = new List<Vector2>();

        var realCenter = WorldGen.ChunkToWorldCoordsCentered(coords.Floored());

        while (enumerator.MoveNext(out var debrisPoint))
        {
            var worldPoint = realCenter + debrisPoint.Value;
            if (_clusterEnabled && !IsWorldPointInCluster(worldPoint))
                continue;

            debrisPoints.Add(worldPoint);
        }

        return debrisPoints;
    }

    public bool ChunkIntersectsCluster(Vector2i chunkCoords)
    {
        if (!_clusterEnabled || _clusterSpacing <= 0f || _clusterRadius <= 0f)
            return true;

        var origin = WorldGen.ChunkToWorldCoords(chunkCoords);
        var chunkBox = new Box2(origin, origin + new Vector2(WorldGen.ChunkSize, WorldGen.ChunkSize));
        return ClusterIntersects(chunkBox);
    }

    private bool IsWorldPointInCluster(Vector2 worldPos)
    {
        if (!_clusterEnabled || _clusterSpacing <= 0f || _clusterRadius <= 0f)
            return true;

        var radiusSq = _clusterRadius * _clusterRadius;
        var cx = (int) MathF.Floor(worldPos.X / _clusterSpacing);
        var cy = (int) MathF.Floor(worldPos.Y / _clusterSpacing);

        for (var x = cx - 1; x <= cx + 1; x++)
        {
            for (var y = cy - 1; y <= cy + 1; y++)
            {
                if (Vector2.DistanceSquared(worldPos, GetClusterCenter(new Vector2i(x, y))) <= radiusSq)
                    return true;
            }
        }

        return false;
    }

    private bool ClusterIntersects(Box2 box)
    {
        var search = _clusterRadius + _clusterSpacing * MathF.Max(0f, _clusterJitter);
        var minCell = new Vector2i(
            (int) MathF.Floor((box.Left - search) / _clusterSpacing),
            (int) MathF.Floor((box.Bottom - search) / _clusterSpacing));
        var maxCell = new Vector2i(
            (int) MathF.Floor((box.Right + search) / _clusterSpacing),
            (int) MathF.Floor((box.Top + search) / _clusterSpacing));

        for (var x = minCell.X; x <= maxCell.X; x++)
        {
            for (var y = minCell.Y; y <= maxCell.Y; y++)
            {
                if (CircleIntersects(GetClusterCenter(new Vector2i(x, y)), _clusterRadius, box))
                    return true;
            }
        }

        return false;
    }

    private Vector2 GetClusterCenter(Vector2i cell)
    {
        var origin = new Vector2(cell.X * _clusterSpacing, cell.Y * _clusterSpacing);
        var jitter = _clusterSpacing * Math.Clamp(_clusterJitter, 0f, 0.45f);
        var hash = HashClusterCell(cell);
        var ox = ((hash & 0xFFFF) / 65535f - 0.5f) * 2f * jitter;
        var oy = (((hash >> 16) & 0xFFFF) / 65535f - 0.5f) * 2f * jitter;
        return origin + new Vector2(ox, oy);
    }

    private static uint HashClusterCell(Vector2i cell)
    {
        unchecked
        {
            var h = (uint) (cell.X * 374761393 + cell.Y * 668265263 + 12345);
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }
    }

    private static bool CircleIntersects(Vector2 center, float radius, Box2 box)
    {
        var closest = new Vector2(
            Math.Clamp(center.X, box.Left, box.Right),
            Math.Clamp(center.Y, box.Bottom, box.Top));
        return Vector2.DistanceSquared(center, closest) <= radius * radius;
    }

    private int PlaceDebrisPoints(
        EntityUid uid,
        DebrisFeaturePlacerControllerComponent component,
        EntityUid chunkUid,
        EntityUid chunkMap,
        MapId mapId,
        List<Vector2> points,
        int startIndex,
        int budget,
        bool pregen)
    {
        var safetyBounds = Box2.UnitCentered.Enlarged(component.SafetyZoneRadius);
        var densityChannel = component.DensityNoiseChannel;
        var failures = 0;
        var processed = 0;

        for (var i = startIndex; i < points.Count; i++)
        {
            if (processed >= budget)
                return i;

            var point = points[i];

            if (component.OwnedDebris.TryGetValue(point, out var existing))
            {
                DebugTools.Assert(Exists(existing));
                continue;
            }

            var pointDensity = _noiseIndex.Evaluate(uid, densityChannel, WorldGen.WorldToChunkCoords(point));
            if (pointDensity == 0 && component.DensityClip || _random.Prob(component.RandomCancellationChance))
                continue;

            if (HasCollisions(mapId, safetyBounds.Translated(point)))
                continue;

            var coords = new EntityCoordinates(chunkMap, point);

            var preEv = new PrePlaceDebrisFeatureEvent(coords, chunkUid);
            RaiseLocalEvent(uid, ref preEv);
            if (uid != chunkUid)
                RaiseLocalEvent(chunkUid, ref preEv);

            if (preEv.Handled)
                continue;

            var debrisFeatureEv = new TryGetPlaceableDebrisFeatureEvent(coords, chunkUid);
            RaiseLocalEvent(uid, ref debrisFeatureEv);

            if (debrisFeatureEv.DebrisProto == null && uid != chunkUid)
                RaiseLocalEvent(chunkUid, ref debrisFeatureEv);

            if (debrisFeatureEv.DebrisProto is not { } proto)
            {
                failures++;
                continue;
            }

            if (pregen && !IsPregenShellPrototype(proto))
            {
                component.DeferredDebris[point] = proto;
                continue;
            }

            SpawnPlacedDebris(uid, component, chunkMap, point, proto, pregen);
            processed++;
        }

        if (failures > 0)
            _sawmill.Error($"Failed to place {failures} debris at chunk {chunkUid}");

        return points.Count;
    }

    private void SpawnDeferredDebris(EntityUid uid, DebrisFeaturePlacerControllerComponent component, EntityUid chunkUid)
    {
        if (component.DeferredDebris.Count == 0)
            return;

        if (!TryComp<WorldChunkComponent>(chunkUid, out var chunk))
            return;

        var chunkMap = chunk.Map;
        if (!HasComp<MapComponent>(chunkMap))
            return;

        foreach (var (point, proto) in component.DeferredDebris)
        {
            if (component.OwnedDebris.TryGetValue(point, out var existing) &&
                existing is not null &&
                !Deleted(existing.Value))
                continue;

            SpawnPlacedDebris(uid, component, chunkMap, point, proto, pregen: false);
        }
    }

    private void SpawnPlacedDebris(
        EntityUid uid,
        DebrisFeaturePlacerControllerComponent component,
        EntityUid chunkMap,
        Vector2 point,
        string proto,
        bool pregen)
    {
        var ent = Spawn(proto, new EntityCoordinates(chunkMap, point));
        component.OwnedDebris[point] = ent;

        var owned = EnsureComp<OwnedDebrisComponent>(ent);
        owned.OwningController = uid;
        owned.LastKey = point;
        EnsureComp<SpaceDebrisComponent>(ent);
        if (HasComp<MapGridComponent>(ent) && !_gridAccess.HasAnyGridType(ent))
            _gridAccess.EnsureGridType(ent, _gridAccess.ResolveGridType(ent));

        if (!pregen)
            return;

        var pregenDebris = EnsureComp<PregenDebrisComponent>(ent);
        pregenDebris.AwaitingLocality = true;
        EnsureComp<CleanupImmuneComponent>(ent);
        RemComp<LocalityLoaderComponent>(ent);
    }

    private void ArmPregenLocality(DebrisFeaturePlacerControllerComponent component)
    {
        foreach (var (_, debris) in component.OwnedDebris)
        {
            if (debris is null || Deleted(debris.Value))
                continue;

            if (!TryComp<PregenDebrisComponent>(debris.Value, out var pregen) || !pregen.AwaitingLocality)
                continue;

            EnsureComp<LocalityLoaderComponent>(debris.Value);
        }
    }

    private void DisarmPregenLocality(EntityUid debris)
    {
        if (!TryComp<PregenDebrisComponent>(debris, out var pregen) || !pregen.AwaitingLocality)
            return;

        RemComp<LocalityLoaderComponent>(debris);
    }

    private bool IsPregenShellPrototype(string protoId)
    {
        return _protos.TryIndex(protoId, out EntityPrototype? proto) &&
               proto.Components.ContainsKey(_mapGridCompName);
    }

    private bool IsInsidePregenRadius(Vector2i chunkCoords)
    {
        var radius = MathF.Max(0f, _debrisPregenRadius);
        return WorldGen.ChunkToWorldCoordsCentered(chunkCoords).LengthSquared() <= radius * radius;
    }
}

/// <summary>
///     Fired directed on the debris feature placer controller and the chunk, ahead of placing a debris piece.
/// </summary>
[ByRefEvent]
[PublicAPI]
public record struct PrePlaceDebrisFeatureEvent(EntityCoordinates Coords, EntityUid Chunk, bool Handled = false);

/// <summary>
///     Fired directed on the debris feature placer controller and the chunk, to select which debris piece to place.
/// </summary>
[ByRefEvent]
[PublicAPI]
public record struct TryGetPlaceableDebrisFeatureEvent(EntityCoordinates Coords, EntityUid Chunk,
    string? DebrisProto = null);
