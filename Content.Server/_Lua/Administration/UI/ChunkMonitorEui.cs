// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaCorp
// See AGPLv3.txt for details.
using Content.Server._NF.Worldgen.Components.Debris;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Server.Shuttles.Components;
using Content.Server.Station.Components;
using Content.Server.Worldgen;
using Content.Server.Worldgen.Components;
using Content.Server.Worldgen.Components.GC;
using Content.Shared._Lua.Administration.ChunkMonitor;
using Content.Shared._Mono.Ships.Components;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared.Administration;
using Content.Shared.Eui;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using System.Linq;
using System.Numerics;

namespace Content.Server._Lua.Administration.UI;

public sealed class ChunkMonitorEui : BaseEui
{
    [Dependency] private readonly IAdminManager _admins = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IMapManager _mapMan = default!;
    private TransformSystem _xform = default!;
    private EntityQuery<WorldControllerComponent> _controllerQuery;
    private EntityQuery<LoadedChunkComponent> _loadedQuery;
    private EntityQuery<ChunkEvictionComponent> _evictQuery;
    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<ActorComponent> _actorQuery;
    private EntityQuery<ShuttleComponent> _serverShuttleQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<BecomesStationComponent> _becomesStationQuery;
    private EntityQuery<GCAbleObjectComponent> _gcAbleQuery;
    private EntityQuery<SpaceDebrisComponent> _spaceDebrisQuery;
    private EntityQuery<VesselComponent> _vesselQuery;
    private EntityQuery<ShuttleDeedComponent> _shuttleDeedQuery;
    private NetEntity _selectedMap = NetEntity.Invalid;
    private readonly Dictionary<EntityUid, int> _deletedByMapSession = new();
    private ChunkMonitorChunkInfo[] _chunks = [];
    private int _loadedCount;
    private int _unloadedCount;

    public ChunkMonitorEui()
    {
        IoCManager.InjectDependencies(this);
        _xform = _entMan.System<TransformSystem>();
        _controllerQuery = _entMan.GetEntityQuery<WorldControllerComponent>();
        _loadedQuery = _entMan.GetEntityQuery<LoadedChunkComponent>();
        _evictQuery = _entMan.GetEntityQuery<ChunkEvictionComponent>();
        _xformQuery = _entMan.GetEntityQuery<TransformComponent>();
        _actorQuery = _entMan.GetEntityQuery<ActorComponent>();
        _serverShuttleQuery = _entMan.GetEntityQuery<ShuttleComponent>();
        _gridQuery = _entMan.GetEntityQuery<MapGridComponent>();
        _becomesStationQuery = _entMan.GetEntityQuery<BecomesStationComponent>();
        _gcAbleQuery = _entMan.GetEntityQuery<GCAbleObjectComponent>();
        _spaceDebrisQuery = _entMan.GetEntityQuery<SpaceDebrisComponent>();
        _vesselQuery = _entMan.GetEntityQuery<VesselComponent>();
        _shuttleDeedQuery = _entMan.GetEntityQuery<ShuttleDeedComponent>();
    }

    public override void Opened()
    {
        base.Opened();
        if (!EnsureAuthorized())
            return;
        var maps = GetMaps();
        if (maps.Length == 0)
        {
            Close();
            return;
        }
        var preferred = Player.AttachedEntity;
        if (preferred != null && _xformQuery.TryGetComponent(preferred.Value, out var xform) && xform.MapUid is { } mapUid)
            _selectedMap = _entMan.GetNetEntity(mapUid);
        if (!_selectedMap.IsValid() && maps.Length > 0)
            _selectedMap = maps[0].MapUid;
        _chunks = [];
        _loadedCount = 0;
        _unloadedCount = 0;
        StateDirty();
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);
        if (!EnsureAuthorized())
            return;
        switch (msg)
        {
            case ChunkMonitorEuiMsg.RequestMapData req:
                if (!_entMan.TryGetEntity(req.MapUid, out EntityUid? mapUidNullable) ||
                    mapUidNullable is not { } mapUid ||
                    !_controllerQuery.HasComponent(mapUid))
                    return;
                _selectedMap = req.MapUid;
                RefreshMapData(mapUid);
                StateDirty();
                break;
            case ChunkMonitorEuiMsg.DeleteChunks del:
                {
                    if (!_entMan.TryGetEntity(del.MapUid, out EntityUid? mapEntityNullable) ||
                        mapEntityNullable is not { } mapEntityUid ||
                        !_controllerQuery.TryGetComponent(mapEntityUid, out var controller))
                        return;
                    var deleted = 0;
                    foreach (var coords in del.Chunks)
                    {
                        if (!controller.Chunks.TryGetValue(coords, out var chunkUid))
                            continue;
                        if (_loadedQuery.HasComponent(chunkUid))
                            _entMan.RemoveComponent<LoadedChunkComponent>(chunkUid);
                        if (_evictQuery.TryGetComponent(chunkUid, out var evict))
                            evict.EvictAt = TimeSpan.Zero;
                        _entMan.QueueDeleteEntity(chunkUid);
                        deleted++;
                    }
                    if (deleted > 0)
                    {
                        _deletedByMapSession.TryGetValue(mapEntityUid, out var prev);
                        _deletedByMapSession[mapEntityUid] = prev + deleted;
                    }
                    if (_selectedMap == del.MapUid)
                    {
                        RefreshMapData(mapEntityUid);
                        StateDirty();
                    }
                    break;
                }
            case ChunkMonitorEuiMsg.PurgeChunks purge:
                {
                    if (!_entMan.TryGetEntity(purge.MapUid, out EntityUid? mapEntityNullable) ||
                        mapEntityNullable is not { } mapEntityUid ||
                        !_controllerQuery.HasComponent(mapEntityUid) ||
                        !_entMan.TryGetComponent(mapEntityUid, out MapComponent? mapComp))
                        return;
                    foreach (var coords in purge.Chunks)
                    {
                        PurgeChunk(mapEntityUid, mapComp.MapId, coords);
                    }
                    if (_selectedMap == purge.MapUid)
                    {
                        RefreshMapData(mapEntityUid);
                        StateDirty();
                    }
                    break;
                }
        }
    }

    public override EuiStateBase GetNewState()
    {
        var maps = GetMaps();
        var deletedCount = 0;
        if (_entMan.TryGetEntity(_selectedMap, out EntityUid? mapUidNullable) && mapUidNullable is { } mapUid)
            _deletedByMapSession.TryGetValue(mapUid, out deletedCount);
        return new ChunkMonitorEuiState(
            _selectedMap,
            maps,
            _chunks,
            _loadedCount,
            _unloadedCount,
            deletedCount);
    }

    private bool EnsureAuthorized()
    {
        if (_admins.HasAdminFlag(Player, AdminFlags.Admin))
            return true;
        Close();
        return false;
    }
    private ChunkMonitorMapInfo[] GetMaps()
    {
        var list = new List<ChunkMonitorMapInfo>();
        var e = _entMan.EntityQueryEnumerator<MapComponent, MetaDataComponent>();
        while (e.MoveNext(out var uid, out var mapComp, out var meta))
        {
            if (mapComp.MapId == MapId.Nullspace)
                continue;
            var name = meta.EntityName;
            list.Add(new ChunkMonitorMapInfo(_entMan.GetNetEntity(uid), name));
        }
        return list.OrderBy(m => m.Name).ToArray();
    }
    private void RefreshMapData(EntityUid mapUid)
    {
        if (!_controllerQuery.TryGetComponent(mapUid, out var controller))
        {
            _chunks = [];
            _loadedCount = 0;
            _unloadedCount = 0;
            return;
        }
        var chunkInfos = new List<ChunkMonitorChunkInfo>(controller.Chunks.Count);
        _loadedCount = 0;
        _unloadedCount = 0;
        var entityCountByChunk = new Dictionary<Vector2i, int>();
        var playerCountByChunk = new Dictionary<Vector2i, int>();
        var shuttleCountByChunk = new Dictionary<Vector2i, int>();
        var stationCountByChunk = new Dictionary<Vector2i, int>();
        var debrisCountByChunk = new Dictionary<Vector2i, int>();
        var gridCountByChunk = new Dictionary<Vector2i, int>();
        var xformEnum = _entMan.EntityQueryEnumerator<TransformComponent>();
        while (xformEnum.MoveNext(out var uid, out var xform))
        {
            if (xform.MapUid is not { } xformMapUid || xformMapUid != mapUid)
                continue;
            if (_entMan.HasComponent<WorldChunkComponent>(uid))
                continue;
            var worldPos = _xform.GetWorldPosition(xform);
            var chunk = WorldGen.WorldToChunkCoords(worldPos).Floored();
            entityCountByChunk.TryGetValue(chunk, out var ec);
            entityCountByChunk[chunk] = ec + 1;
            if (_actorQuery.HasComponent(uid))
            {
                playerCountByChunk.TryGetValue(chunk, out var pc);
                playerCountByChunk[chunk] = pc + 1;
            }
            if (_gridQuery.HasComponent(uid))
            {
                if (_becomesStationQuery.HasComponent(uid))
                {
                    stationCountByChunk.TryGetValue(chunk, out var stc);
                    stationCountByChunk[chunk] = stc + 1;
                }
                else if (_gcAbleQuery.HasComponent(uid) || _spaceDebrisQuery.HasComponent(uid))
                {
                    debrisCountByChunk.TryGetValue(chunk, out var dc);
                    debrisCountByChunk[chunk] = dc + 1;
                }
                else if (_serverShuttleQuery.HasComponent(uid) && _vesselQuery.HasComponent(uid) && _shuttleDeedQuery.HasComponent(uid))
                {
                    shuttleCountByChunk.TryGetValue(chunk, out var sc);
                    shuttleCountByChunk[chunk] = sc + 1;
                }
                else
                {
                    gridCountByChunk.TryGetValue(chunk, out var gc);
                    gridCountByChunk[chunk] = gc + 1;
                }
            }
        }
        foreach (var (coords, chunkUid) in controller.Chunks)
        {
            var loaded = _loadedQuery.HasComponent(chunkUid);
            var status = loaded ? ChunkMonitorChunkStatus.Loaded : ChunkMonitorChunkStatus.Unloaded;
            if (loaded) _loadedCount++; else _unloadedCount++;
            entityCountByChunk.TryGetValue(coords, out var entities);
            playerCountByChunk.TryGetValue(coords, out var players);
            shuttleCountByChunk.TryGetValue(coords, out var shuttles);
            stationCountByChunk.TryGetValue(coords, out var stations);
            debrisCountByChunk.TryGetValue(coords, out var debris);
            gridCountByChunk.TryGetValue(coords, out var grids);
            chunkInfos.Add(new ChunkMonitorChunkInfo(coords, status, entities, players, shuttles, stations, debris, grids));
        }
        _chunks = chunkInfos.ToArray();
    }
    private void PurgeChunk(EntityUid mapUid, MapId mapId, Vector2i coords)
    {
        var worldMin = new Vector2(coords.X * WorldGen.ChunkSize, coords.Y * WorldGen.ChunkSize);
        var worldMax = worldMin + new Vector2(WorldGen.ChunkSize, WorldGen.ChunkSize);
        var box = new Box2(worldMin, worldMax);
        var grids = new List<Entity<MapGridComponent>>();
        _mapMan.FindGridsIntersecting(mapId, box, ref grids, approx: true, includeMap: false);
        foreach (var grid in grids)
        {
            if (grid.Owner == mapUid)
                continue;
            _entMan.QueueDeleteEntity(grid.Owner);
        }
        var q = _entMan.EntityQueryEnumerator<TransformComponent>();
        while (q.MoveNext(out var uid, out var xform))
        {
            if (uid == mapUid)
                continue;
            if (xform.MapUid is not { } xformMapUid || xformMapUid != mapUid)
                continue;
            if (_entMan.HasComponent<WorldChunkComponent>(uid))
                continue;
            if (_gridQuery.HasComponent(uid))
                continue;
            var wp = _xform.GetWorldPosition(xform);
            if (!box.Contains(wp))
                continue;
            _entMan.QueueDeleteEntity(uid);
        }
    }
}
