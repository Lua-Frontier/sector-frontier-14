// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using Content.Server.Singularity.EntitySystems;
using Content.Server.Singularity.Events;
using Content.Server.Temperature.Systems;
using Content.Shared._Lua.AmbientSpaceEffects;
using Content.Shared._Lua.SpaceHazards;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Maps;
using Content.Shared.Singularity.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server._Lua.SpaceHazards;

public sealed class SectorBlackHoleSystem : EntitySystem
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(0.5);

    private const int BaseMaxHits = 20;

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EventHorizonSystem _eventHorizon = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SpaceHazardActivitySystem _activity = default!;
    [Dependency] private readonly TemperatureSystem _temperature = default!;

    private TimeSpan _nextTick;
    private readonly List<Entity<MapGridComponent>> _gridScratch = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SectorCelestialBodyComponent, EventHorizonAttemptConsumeEntityEvent>(EventHorizonSystem.PreventConsume);
        SubscribeLocalEvent<SectorBackgroundPlanetComponent, EventHorizonAttemptConsumeEntityEvent>(EventHorizonSystem.PreventConsume);
        SubscribeLocalEvent<AmbientSpaceFieldComponent, EventHorizonAttemptConsumeEntityEvent>(EventHorizonSystem.PreventConsume);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextTick)
            return;

        var dt = (float) TickInterval.TotalSeconds;
        _nextTick = now + TickInterval;

        foreach (var uid in _activity.ActiveHazards)
        {
            if (!TryComp(uid, out SectorCelestialBodyComponent? body) || body.Kind != CelestialKind.BlackHole)
                continue;

            if (!TryComp(uid, out EventHorizonComponent? horizon))
                continue;

            if (!TryComp(uid, out TransformComponent? xform) || xform.MapID == MapId.Nullspace)
                continue;

            var pos = _transform.GetWorldPosition(xform);
            var pullR = MathF.Max(body.PullRadius, 1f);
            var horizonR = MathF.Max(body.EventHorizonRadius, 1f);
            var hazardR = MathF.Max(body.HazardRadius, 1f);
            var scanR = MathF.Max(MathF.Max(pullR, horizonR), hazardR);
            var box = Box2.CenteredAround(pos, new Vector2(scanR * 2f, scanR * 2f));
            var grids = _gridScratch;
            grids.Clear();
            _mapManager.FindGridsIntersecting(xform.MapID, box, ref grids, approx: true, includeMap: false);

            foreach (var grid in grids)
            {
                var gridUid = grid.Owner;
                var distClosest = DistanceToGrid(gridUid, grid.Comp, pos);

                if (distClosest <= pullR && TryComp<PhysicsComponent>(gridUid, out var physics))
                {
                    var gridPos = _transform.GetWorldPosition(gridUid);
                    var toHole = pos - gridPos;
                    var distCenter = toHole.Length();
                    var vel = physics.LinearVelocity;

                    if (distCenter < 0.05f)
                    {
                        vel *= 0.35f;
                    }
                    else
                    {
                        var dir = toHole / distCenter;
                        var t = Math.Clamp(1f - distCenter / pullR, 0f, 1f);
                        var strength = body.PullAcceleration * (0.2f * t + 0.8f * t * t);
                        vel += dir * strength * dt;

                        var outward = Vector2.Dot(vel, -dir);
                        if (outward > 0f)
                            vel += dir * outward;
                    }

                    _physics.SetLinearVelocity(gridUid, vel, body: physics);
                }

                if (distClosest > horizonR)
                    continue;

                var factor = SectorCelestialProximity.Factor(distClosest, horizonR);
                var hits = SectorCelestialProximity.ScaledHits(BaseMaxHits, factor);
                if (hits > 0)
                {
                    SectorCelestialHullDamage.DamageInRadius(
                        gridUid,
                        grid.Comp,
                        pos,
                        horizonR,
                        body.HorizonDamage * factor,
                        hits,
                        _maps,
                        _turf,
                        _damageable,
                        EntityManager,
                        _random);
                }

                var circle = new Circle(pos, horizonR);
                _eventHorizon.AttemptConsumeTiles(
                    uid,
                    _maps.GetTilesIntersecting(gridUid, grid.Comp, circle),
                    gridUid,
                    grid.Comp,
                    horizon);
            }

            SectorCelestialMobDamage.PullUncontainedMobs(
                xform.MapID,
                pos,
                pullR,
                body.PullAcceleration,
                dt,
                _lookup,
                _transform,
                _physics,
                EntityManager);
            SectorCelestialMobDamage.ApplyHazardToMobsInRadius(
                xform.MapID,
                pos,
                hazardR,
                body.MobHazardDamage,
                _lookup,
                _transform,
                _damageable,
                _temperature,
                EntityManager);
            ConsumeHardCollidableInRange(uid, horizonR, horizon);
        }
    }

    private void ConsumeHardCollidableInRange(EntityUid uid, float range, EventHorizonComponent horizon)
    {
        if (!TryComp(uid, out PhysicsComponent? body))
            return;

        foreach (var entity in _lookup.GetEntitiesInRange(uid, range, flags: LookupFlags.Uncontained))
        {
            if (entity == uid)
                continue;

            if (!TryComp(entity, out PhysicsComponent? otherBody))
                continue;

            if (!_physics.IsHardCollidable((uid, null, body), (entity, null, otherBody)))
                continue;

            _eventHorizon.AttemptConsumeEntity(uid, entity, horizon);
        }
    }

    private float DistanceToGrid(EntityUid gridUid, MapGridComponent grid, Vector2 worldPoint)
    {
        var inv = _transform.GetInvWorldMatrix(gridUid);
        var local = Vector2.Transform(worldPoint, inv);
        var aabb = grid.LocalAABB;
        var closest = Vector2.Clamp(local, aabb.BottomLeft, aabb.TopRight);
        var closestWorld = Vector2.Transform(closest, _transform.GetWorldMatrix(gridUid));
        return (closestWorld - worldPoint).Length();
    }
}
