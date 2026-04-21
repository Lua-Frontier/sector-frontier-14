// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server._Lua.Stargate.Components;
using Content.Server._Lua.Stargate.Events;
using Content.Shared._Lua.Stargate.Components;
using Content.Shared.Ghost;
using Content.Shared.Lua.CLVar;
using Content.Shared.Mind.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Lua.Stargate.Systems;

public sealed class StargateMapFreezeSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly StargateAddressRegistrySystem _registry = default!;
    [Dependency] private readonly StargateWorldPersistenceSystem _persistence = default!;

    private float _checkAccumulator;

    private EntityQuery<ActorComponent> _actorQuery;
    private EntityQuery<GhostComponent> _ghostQuery;
    private EntityQuery<MindContainerComponent> _mindContainerQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<StargatePortalTimerComponent> _portalTimerQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    private static readonly HashSet<string> CryoPodPrototypeIds = new(StringComparer.Ordinal)
    {
        "MachineCryoSleepPod",
        "MachineCryoSleepPodPlayer",
        "MachineCryoSleepPodFallback",
        "CryogenicSleepUnit",
        "CryogenicSleepUnitSpawner",
        "CryogenicSleepUnitSpawnerLateJoin",
    };

    public override void Initialize()
    {
        base.Initialize();
        _actorQuery = GetEntityQuery<ActorComponent>();
        _ghostQuery = GetEntityQuery<GhostComponent>();
        _mindContainerQuery = GetEntityQuery<MindContainerComponent>();
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
        var checkIntervalSeconds = _cfg.GetCVar(CLVars.StargateWorldFreezeCheckIntervalSeconds);
        if (_checkAccumulator < checkIntervalSeconds)
            return;

        _checkAccumulator -= checkIntervalSeconds;

        var curTime = _timing.CurTime;
        var freezeDelaySeconds = _cfg.GetCVar(CLVars.StargateWorldFreezeDelaySeconds);
        var freezeDelay = TimeSpan.FromSeconds(freezeDelaySeconds);
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

                if (!dest.Frozen && curTime - dest.EmptySince.Value >= freezeDelay)
                    Freeze(uid, dest);
                else if (dest.Frozen && _cfg.GetCVar(CLVars.StargateWorldSavesEnabled))
                {
                    var saveAfterSeconds = _cfg.GetCVar(CLVars.StargateWorldSaveAfterFrozenMinutes) * 60;
                    var totalEmptySeconds = (curTime - dest.EmptySince.Value).TotalSeconds;
                    if (totalEmptySeconds >= freezeDelay.TotalSeconds + saveAfterSeconds) TrySaveAndUnloadWorld(uid, dest);
                }
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

    private bool MapHasUnsettledMind(EntityUid mapUid)
    {
        var query = AllEntityQuery<MindContainerComponent, TransformComponent>();
        while (query.MoveNext(out _, out var mindContainer, out var xform))
        {
            if (xform.MapUid != mapUid) continue;
            if (mindContainer.HasMind) return true;
        }
        return false;
    }

    private bool MapHasCryoPod(EntityUid mapUid)
    {
        var query = AllEntityQuery<MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out _, out var meta, out var xform))
        {
            if (xform.MapUid != mapUid) continue;
            if (meta.EntityPrototype?.ID is { } id && CryoPodPrototypeIds.Contains(id)) return true;
        }
        return false;
    }

    public void Freeze(EntityUid mapUid, StargateDestinationComponent dest)
    {
        _meta.SetEntityPaused(mapUid, true);

        if (_xformQuery.TryGetComponent(mapUid, out var xform))
        {
            dest.FrozenCollidables.Clear();
            RecursiveSetPaused(xform, true, dest.FrozenCollidables);
        }

        dest.Frozen = true;
    }

    public void Unfreeze(EntityUid mapUid, StargateDestinationComponent dest)
    {
        _meta.SetEntityPaused(mapUid, false);

        if (_xformQuery.TryGetComponent(mapUid, out var xform))
            RecursiveSetPaused(xform, false, null);

        var hadFrozenCollidables = dest.FrozenCollidables.Count > 0;
        foreach (var uid in dest.FrozenCollidables)
        {
            if (_physicsQuery.TryGetComponent(uid, out var body)) _physics.SetCanCollide(uid, true, body: body);
        }
        dest.FrozenCollidables.Clear();
        if (!hadFrozenCollidables && _xformQuery.TryGetComponent(mapUid, out var mapXform)) RecursiveRestoreCollision(mapXform);
        dest.Frozen = false;
        dest.EmptySince = null;
    }

    private void RecursiveRestoreCollision(TransformComponent xform)
    {
        var enumerator = xform.ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            if (_physicsQuery.TryGetComponent(child, out var body) && !body.CanCollide) _physics.SetCanCollide(child, true, body: body);
            if (_xformQuery.TryGetComponent(child, out var childXform)) RecursiveRestoreCollision(childXform);
        }
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

    public void TrySaveAndUnloadWorld(EntityUid mapUid, StargateDestinationComponent dest)
    {
        var address = dest.Address;
        if (address == null || address.Length == 0) return;
        if (MapHasUnsettledMind(mapUid)) return;
        if (MapHasCryoPod(mapUid)) return;
        var key = StargateAddressRegistrySystem.AddressToKey(address);
        var path = StargateWorldPersistenceSystem.GetSavePath(key);
        if (!_persistence.TrySaveStargateWorld(mapUid, path)) return;
        _registry.UnregisterDestination(address);
        var mapId = Transform(mapUid).MapID;
        _mapSystem.DeleteMap(mapId);
    }
}
