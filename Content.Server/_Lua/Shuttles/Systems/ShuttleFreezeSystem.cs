// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server._Lua.Shuttles.Components;
using Content.Server.Shuttles.Components;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared.Ghost;
using Content.Shared.Lua.CLVar;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server._Lua.Shuttles.Systems;

public sealed class ShuttleFreezeSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;

    private bool _enabled;
    private TimeSpan _freezeDelay;
    private float _freezeCheckInterval;
    private float _unfreezeCheckInterval;
    private float _proximityTiles;

    private float _freezeAccumulator;
    private float _unfreezeAccumulator;

    private EntityQuery<ActorComponent> _actorQuery;
    private EntityQuery<GhostComponent> _ghostQuery;
    private EntityQuery<FTLComponent> _ftlQuery;
    private EntityQuery<ShuttleFreezeStateComponent> _freezeQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    private List<Entity<MapGridComponent>> _nearbyGrids = new();

    public override void Initialize()
    {
        base.Initialize();

        _actorQuery = GetEntityQuery<ActorComponent>();
        _ghostQuery = GetEntityQuery<GhostComponent>();
        _ftlQuery = GetEntityQuery<FTLComponent>();
        _freezeQuery = GetEntityQuery<ShuttleFreezeStateComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        Subs.CVar(_cfg, CLVars.ShuttleFreezeEnabled, val => _enabled = val, true);
        Subs.CVar(_cfg, CLVars.ShuttleFreezeDelay, val => _freezeDelay = TimeSpan.FromMinutes(val), true);
        Subs.CVar(_cfg, CLVars.ShuttleFreezeCheckInterval, val => _freezeCheckInterval = val, true);
        Subs.CVar(_cfg, CLVars.ShuttleFreezeProximityTiles, val => _proximityTiles = val, true);
        Subs.CVar(_cfg, CLVars.ShuttleFreezeUnfreezeInterval, val => _unfreezeCheckInterval = val, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_enabled)
            return;

        _freezeAccumulator += frameTime;
        _unfreezeAccumulator += frameTime;

        if (_unfreezeAccumulator >= _unfreezeCheckInterval)
        {
            _unfreezeAccumulator -= _unfreezeCheckInterval;
            UpdateProximityUnfreeze();
        }

        if (_freezeAccumulator >= _freezeCheckInterval)
        {
            _freezeAccumulator -= _freezeCheckInterval;
            UpdateFreeze();
        }
    }

    private void UpdateProximityUnfreeze()
    {
        var playerQuery = AllEntityQuery<ActorComponent, TransformComponent>();

        while (playerQuery.MoveNext(out var playerUid, out _, out var playerXform))
        {
            if (_ghostQuery.HasComp(playerUid))
                continue;

            if (playerXform.GridUid is { } gridUid &&
                _freezeQuery.TryGetComponent(gridUid, out var directState) &&
                directState.Frozen)
            {
                Unfreeze(gridUid, directState);
            }

            var mapPos = _transformSystem.GetMapCoordinates(playerUid, playerXform);
            if (mapPos.MapId == MapId.Nullspace)
                continue;

            var range = new Vector2(_proximityTiles, _proximityTiles);
            var searchBox = new Box2(mapPos.Position - range, mapPos.Position + range);

            _nearbyGrids.Clear();
            _mapManager.FindGridsIntersecting(mapPos.MapId, searchBox, ref _nearbyGrids, approx: true, includeMap: false);

            foreach (var nearbyGrid in _nearbyGrids)
            {
                if (!_freezeQuery.TryGetComponent(nearbyGrid.Owner, out var state) || !state.Frozen)
                    continue;

                if (!_xformQuery.TryGetComponent(nearbyGrid.Owner, out var gridXform))
                    continue;

                var (worldPos, worldRot) = _transformSystem.GetWorldPositionRotation(gridXform);
                var worldAABB = new Box2Rotated(
                    nearbyGrid.Comp.LocalAABB.Translated(worldPos),
                    worldRot,
                    worldPos
                ).CalcBoundingBox();

                var closestPoint = worldAABB.ClosestPoint(mapPos.Position);
                var distance = (mapPos.Position - closestPoint).Length();

                if (distance <= _proximityTiles)
                    Unfreeze(nearbyGrid.Owner, state);
            }
        }
    }

    private void UpdateFreeze()
    {
        var curTime = _timing.CurTime;
        var query = AllEntityQuery<ShuttleComponent, ShuttleDeedComponent, MapGridComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out _, out _, out _, out _))
        {
            if (HasComp<MapComponent>(uid))
                continue;

            var state = EnsureComp<ShuttleFreezeStateComponent>(uid);

            if (_ftlQuery.HasComp(uid))
            {
                if (state.Frozen)
                    Unfreeze(uid, state);
                else
                    state.EmptySince = null;

                continue;
            }

            if (state.Frozen)
            {
                continue;
            }

            var hasPlayers = GridHasPlayers(uid);

            if (hasPlayers)
            {
                state.EmptySince = null;
            }
            else
            {
                state.EmptySince ??= curTime;

                if (curTime - state.EmptySince.Value >= _freezeDelay)
                    Freeze(uid, state);
            }
        }
    }

    private bool GridHasPlayers(EntityUid gridUid)
    {
        if (!_xformQuery.TryGetComponent(gridUid, out var gridXform))
            return false;

        return RecursiveHasPlayer(gridXform);
    }

    private bool RecursiveHasPlayer(TransformComponent xform)
    {
        var enumerator = xform.ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            if (IsPlayer(child))
                return true;

            if (_xformQuery.TryGetComponent(child, out var childXform) && RecursiveHasPlayer(childXform))
                return true;
        }

        return false;
    }

    private bool IsPlayer(EntityUid uid)
    {
        return _actorQuery.HasComp(uid) && !_ghostQuery.HasComp(uid);
    }

    private void Freeze(EntityUid gridUid, ShuttleFreezeStateComponent state)
    {
        _meta.SetEntityPaused(gridUid, true);

        if (_xformQuery.TryGetComponent(gridUid, out var gridXform))
            RecursiveSetPaused(gridXform, true);

        state.Frozen = true;
    }

    private void Unfreeze(EntityUid gridUid, ShuttleFreezeStateComponent state)
    {
        _meta.SetEntityPaused(gridUid, false);

        if (_xformQuery.TryGetComponent(gridUid, out var gridXform))
            RecursiveSetPaused(gridXform, false);

        state.Frozen = false;
        state.EmptySince = null;
    }

    private void RecursiveSetPaused(TransformComponent xform, bool paused)
    {
        var enumerator = xform.ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            if (IsPlayer(child))
                continue;

            _meta.SetEntityPaused(child, paused);

            if (_xformQuery.TryGetComponent(child, out var childXform))
                RecursiveSetPaused(childXform, paused);
        }
    }
}
