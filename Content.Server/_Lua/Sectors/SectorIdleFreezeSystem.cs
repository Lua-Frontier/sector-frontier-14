// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server._NF.CryoSleep;
using Content.Server._NF.RoundNotifications.Events;
using Content.Server.Shuttles.Events;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Lua.CLVar;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Lua.Sectors;

public sealed class SectorIdleFreezeSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SectorSystem _sectors = default!;
    [Dependency] private readonly StationSystem _station = default!;

    private float _checkAccumulator;
    private bool _roundActive;

    private readonly Dictionary<MapId, TimeSpan> _emptySince = new();
    private readonly Dictionary<MapId, int> _pinCounts = new();
    private readonly Dictionary<EntityUid, MapId> _rulePins = new();

    private EntityQuery<GhostComponent> _ghostQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();
        _ghostQuery = GetEntityQuery<GhostComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundCleanup);
        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnPlayerBeforeSpawn);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<FTLRequestEvent>(OnFTLRequest);
        SubscribeLocalEvent<FTLStartedEvent>(OnFTLStarted);
        SubscribeLocalEvent<CryosleepWakeUpEvent>(OnCryosleepWakeUp);
    }

    private void OnRoundStarted(RoundStartedEvent ev)
    {
        _roundActive = true;
        _checkAccumulator = 0f;
    }

    private void OnRoundCleanup(RoundRestartCleanupEvent ev)
    {
        _roundActive = false;
        _checkAccumulator = 0f;
        _emptySince.Clear();
        _pinCounts.Clear();
        _rulePins.Clear();
    }

    private void OnPlayerBeforeSpawn(PlayerBeforeSpawnEvent ev)
    {
        if (ev.Station.IsValid() && TryComp<StationDataComponent>(ev.Station, out var data))
        {
            var grid = _station.GetLargestGrid(data);
            if (grid != null && _xformQuery.TryGetComponent(grid.Value, out var gridXform))
                EnsureUnfrozen(gridXform.MapID);
        }

        if (_sectors.TryGetHubMapId(out var hub))
            EnsureUnfrozen(hub);
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (_ghostQuery.HasComp(ev.Entity))
            return;

        if (!_xformQuery.TryGetComponent(ev.Entity, out var xform))
            return;

        EnsureUnfrozen(xform.MapID);
    }

    private void OnFTLRequest(ref FTLRequestEvent ev)
    {
        if (!_xformQuery.TryGetComponent(ev.MapUid, out var xform))
            return;

        EnsureUnfrozen(xform.MapID);
    }

    private void OnFTLStarted(ref FTLStartedEvent ev)
    {
        EnsureUnfrozen(_transform.GetMapId(ev.TargetCoordinates));
    }

    private void OnCryosleepWakeUp(CryosleepWakeUpEvent ev)
    {
        if (!_xformQuery.TryGetComponent(ev.Cryopod, out var xform))
            return;

        EnsureUnfrozen(xform.MapID);
    }

    public void EnsureUnfrozen(MapId mapId)
    {
        if (mapId == MapId.Nullspace)
            return;

        if (!_sectors.TryGetSectorId(mapId, out _))
            return;

        _emptySince.Remove(mapId);

        if (!_map.MapExists(mapId))
            return;

        if (_map.IsPaused(mapId))
            _map.SetPaused(mapId, false);
    }

    public void EnsureUnfrozen(EntityUid mapUid)
    {
        if (!_xformQuery.TryGetComponent(mapUid, out var xform))
            return;

        EnsureUnfrozen(xform.MapID);
    }

    public void AddPin(MapId mapId)
    {
        if (mapId == MapId.Nullspace || !_sectors.TryGetSectorId(mapId, out _))
            return;

        EnsureUnfrozen(mapId);
        _pinCounts.TryGetValue(mapId, out var count);
        _pinCounts[mapId] = count + 1;
    }

    public void RemovePin(MapId mapId)
    {
        if (!_pinCounts.TryGetValue(mapId, out var count))
            return;

        count--;
        if (count <= 0)
            _pinCounts.Remove(mapId);
        else
            _pinCounts[mapId] = count;
    }

    public void PinForRule(EntityUid rule, MapId mapId)
    {
        if (mapId == MapId.Nullspace || !_sectors.TryGetSectorId(mapId, out _))
            return;

        if (!_rulePins.TryAdd(rule, mapId))
            return;

        AddPin(mapId);
    }

    public void UnpinForRule(EntityUid rule)
    {
        if (!_rulePins.Remove(rule, out var mapId))
            return;

        RemovePin(mapId);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_roundActive || !_sectors.BootstrapComplete)
            return;

        if (!_cfg.GetCVar(CLVars.SectorIdleFreezeEnabled))
            return;

        _checkAccumulator += frameTime;
        var checkInterval = _cfg.GetCVar(CLVars.SectorIdleFreezeCheckIntervalSeconds);
        if (_checkAccumulator < checkInterval)
            return;

        _checkAccumulator -= checkInterval;

        var curTime = _timing.CurTime;
        var freezeDelay = TimeSpan.FromSeconds(_cfg.GetCVar(CLVars.SectorIdleFreezeDelaySeconds));

        foreach (var (_, mapId, mapUid) in _sectors.EnumerateSectorMaps())
        {
            if (!_map.MapExists(mapId) || !Exists(mapUid))
            {
                _emptySince.Remove(mapId);
                continue;
            }

            var active = IsMapActive(mapId, mapUid);
            if (active)
            {
                _emptySince.Remove(mapId);
                if (_map.IsPaused(mapId))
                    _map.SetPaused(mapId, false);
                continue;
            }

            if (!_emptySince.TryGetValue(mapId, out var emptySince))
            {
                _emptySince[mapId] = curTime;
                continue;
            }

            if (_map.IsPaused(mapId))
                continue;

            if (curTime - emptySince >= freezeDelay)
                _map.SetPaused(mapId, true);
        }
    }

    private bool IsMapActive(MapId mapId, EntityUid mapUid)
    {
        if (_pinCounts.TryGetValue(mapId, out var pins) && pins > 0)
            return true;

        if (MapHasPlayers(mapUid))
            return true;

        if (MapHasInboundFtl(mapId))
            return true;

        return false;
    }

    private bool MapHasPlayers(EntityUid mapUid)
    {
        var query = AllEntityQuery<ActorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapUid != mapUid)
                continue;

            if (_ghostQuery.HasComp(uid))
                continue;

            return true;
        }

        return false;
    }

    private bool MapHasInboundFtl(MapId mapId)
    {
        var query = EntityQueryEnumerator<FTLComponent>();
        while (query.MoveNext(out _, out var ftl))
        {
            if (ftl.LinkedShuttle != null)
                continue;

            if (!ftl.TargetCoordinates.IsValid(EntityManager))
                continue;

            if (_transform.GetMapId(ftl.TargetCoordinates) == mapId)
                return true;
        }

        return false;
    }
}
