// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server._Lua.Stargate.Components;
using Content.Server._Lua.Stargate.Events;
using Content.Shared._Lua.Stargate.Components;
using Content.Shared.Ghost;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Lua.Stargate.Systems;

public sealed class StargateMapFreezeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly TimeSpan FreezeDelay = TimeSpan.FromSeconds(30);
    private float _checkAccumulator;
    private const float CheckInterval = 10f;

    private EntityQuery<ActorComponent> _actorQuery;
    private EntityQuery<GhostComponent> _ghostQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<StargatePortalTimerComponent> _portalTimerQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();
        _actorQuery = GetEntityQuery<ActorComponent>();
        _ghostQuery = GetEntityQuery<GhostComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _portalTimerQuery = GetEntityQuery<StargatePortalTimerComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<StargateDestinationComponent, ComponentStartup>(OnStargateDestStartup);
        SubscribeLocalEvent<StargateDestinationComponent, AttemptStargateOpenEvent>(OnAttemptStargateOpen);
    }

    private void OnStargateDestStartup(Entity<StargateDestinationComponent> ent, ref ComponentStartup args)
    { EnsureComp<StargateMapTagComponent>(ent); }

    private void OnAttemptStargateOpen(Entity<StargateDestinationComponent> ent, ref AttemptStargateOpenEvent args)
    { if (ent.Comp.Frozen) Unfreeze(ent, ent.Comp); }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (_ghostQuery.HasComp(ev.Entity))
            return;

        if (!_xformQuery.TryGetComponent(ev.Entity, out var xform))
            return;

        if (xform.MapUid is not { } mapUid)
            return;

        if (!TryComp<StargateDestinationComponent>(mapUid, out var dest))
            return;

        if (dest.Frozen)
            Unfreeze(mapUid, dest);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _checkAccumulator += frameTime;
        if (_checkAccumulator < CheckInterval)
            return;

        _checkAccumulator -= CheckInterval;

        var curTime = _timing.CurTime;
        var query = AllEntityQuery<StargateDestinationComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var dest, out _))
        {
            var isActive = MapHasPlayers(uid) || HasOpenPortal(dest);

            if (isActive)
            {
                dest.EmptySince = null;

                if (dest.Frozen)
                    Unfreeze(uid, dest);
            }
            else
            {
                dest.EmptySince ??= curTime;

                if (!dest.Frozen && curTime - dest.EmptySince.Value >= FreezeDelay)
                    Freeze(uid, dest);
            }
        }
    }

    private bool HasOpenPortal(StargateDestinationComponent dest)
    { return dest.GateUid is { } gateUid && _portalTimerQuery.HasComp(gateUid); }

    private bool MapHasPlayers(EntityUid mapUid)
    {
        if (!_xformQuery.TryGetComponent(mapUid, out var mapXform))
            return false;

        return RecursiveHasPlayer(mapXform);
    }

    private bool RecursiveHasPlayer(TransformComponent xform)
    {
        var enumerator = xform.ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            if (_actorQuery.HasComp(child) && !_ghostQuery.HasComp(child))
                return true;

            if (_xformQuery.TryGetComponent(child, out var childXform) && RecursiveHasPlayer(childXform))
                return true;
        }

        return false;
    }

    private void Freeze(EntityUid mapUid, StargateDestinationComponent dest)
    {
        _meta.SetEntityPaused(mapUid, true);

        if (_xformQuery.TryGetComponent(mapUid, out var xform))
        {
            dest.FrozenCollidables.Clear();
            RecursiveSetPaused(xform, true, dest.FrozenCollidables);
        }

        dest.Frozen = true;
    }

    private void Unfreeze(EntityUid mapUid, StargateDestinationComponent dest)
    {
        _meta.SetEntityPaused(mapUid, false);

        if (_xformQuery.TryGetComponent(mapUid, out var xform))
            RecursiveSetPaused(xform, false, null);

        foreach (var uid in dest.FrozenCollidables)
        {
            _physics.SetCanCollide(uid, true);
        }

        dest.FrozenCollidables.Clear();
        dest.Frozen = false;
        dest.EmptySince = null;
    }

    private void RecursiveSetPaused(TransformComponent xform, bool paused, HashSet<EntityUid>? frozenCollidables)
    {
        var enumerator = xform.ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            if (paused && _actorQuery.HasComp(child))
                continue;

            _meta.SetEntityPaused(child, paused);

            if (paused && _physicsQuery.TryGetComponent(child, out var body) && body.CanCollide)
            {
                _physics.SetCanCollide(child, false, body: body);
                frozenCollidables?.Add(child);
            }

            if (_xformQuery.TryGetComponent(child, out var childXform))
                RecursiveSetPaused(childXform, paused, frozenCollidables);
        }
    }
}
