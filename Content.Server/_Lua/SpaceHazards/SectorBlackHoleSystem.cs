// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Server._NF.Shuttles.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.StationEvents.Events;
using Content.Server.Temperature.Systems;
using Content.Shared._Lua.SpaceHazards;
using Content.Shared.Damage;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server._Lua.SpaceHazards;

public sealed class SectorBlackHoleSystem : EntitySystem
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(0.25);

    private const int BaseMaxHits = 12;

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SpaceHazardActivitySystem _activity = default!;
    [Dependency] private readonly TemperatureSystem _temperature = default!;
    [Dependency] private readonly LinkedLifecycleGridSystem _linkedLifecycle = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;

    private TimeSpan _nextTick;
    private readonly List<Entity<MapGridComponent>> _gridScratch = new();

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
                if (Deleted(gridUid) || EntityManager.IsQueuedForDeletion(gridUid))
                    continue;

                var distClosest = DistanceToGrid(gridUid, grid.Comp, pos);

                if (distClosest <= pullR)
                {
                    TearForceAnchor(gridUid);

                    if (TryComp<PhysicsComponent>(gridUid, out var physics))
                        ApplyGridPull(gridUid, grid.Comp, physics, pos, distClosest, pullR, horizonR, body.PullAcceleration, dt);
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

                SectorBlackHoleConsume.TryEraseAndMaybeSwallow(
                    uid,
                    gridUid,
                    grid.Comp,
                    pos,
                    horizonR,
                    _maps,
                    _transform,
                    EntityManager,
                    _linkedLifecycle);
            }

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
            SectorBlackHoleConsume.PullAndConsumeFreeEntities(
                uid,
                xform.MapID,
                pos,
                pullR,
                horizonR,
                body.PullAcceleration,
                dt,
                _lookup,
                _transform,
                _physics,
                EntityManager);
        }
    }

    private void TearForceAnchor(EntityUid gridUid)
    {
        var hadLock =
            HasComp<ForceAnchorComponent>(gridUid) ||
            HasComp<ForceAnchorPostFTLComponent>(gridUid) ||
            HasComp<PreventGridAnchorChangesComponent>(gridUid);

        if (!hadLock)
        {
            if (TryComp(gridUid, out PhysicsComponent? physics) &&
                physics.BodyType == BodyType.Static &&
                HasComp<ShuttleComponent>(gridUid))
            {
                _shuttle.Enable(gridUid, component: physics);
            }

            return;
        }

        RemComp<ForceAnchorComponent>(gridUid);
        RemComp<ForceAnchorPostFTLComponent>(gridUid);
        RemComp<PreventGridAnchorChangesComponent>(gridUid);

        if (TryComp(gridUid, out PhysicsComponent? body))
        {
            if (HasComp<ShuttleComponent>(gridUid))
            {
                _shuttle.Enable(gridUid, component: body);
                if (TryComp(gridUid, out ShuttleComponent? shuttle))
                    shuttle.Enabled = true;
            }
            else
            {
                _physics.SetBodyType(gridUid, BodyType.Dynamic, body: body);
                _physics.SetBodyStatus(gridUid, body, BodyStatus.InAir);
                _physics.SetFixedRotation(gridUid, false, body: body);
            }
        }
    }

    private void ApplyGridPull(
        EntityUid gridUid,
        MapGridComponent grid,
        PhysicsComponent physics,
        Vector2 holePos,
        float distClosest,
        float pullR,
        float horizonR,
        float pullAcceleration,
        float dt)
    {
        if (physics.BodyType == BodyType.Static)
            return;

        var worldMatrix = _transform.GetWorldMatrix(gridUid);
        var aabb = grid.LocalAABB;
        var massCenter = Vector2.Transform(aabb.Center, worldMatrix);
        var toHole = holePos - massCenter;
        var distCenter = toHole.Length();
        var hullRadius = new Vector2(aabb.Width, aabb.Height).Length() * 0.5f;

        if (distCenter + hullRadius < 0.75f)
        {
            _physics.SetLinearVelocity(gridUid, Vector2.Zero, body: physics);
            _physics.SetAngularVelocity(gridUid, 0f, body: physics);
            return;
        }

        if (distCenter < 0.05f)
        {
            var inv = _transform.GetInvWorldMatrix(gridUid);
            var localHole = Vector2.Transform(holePos, inv);
            var farLocal = Vector2.Clamp(localHole + (aabb.Center - localHole) * 2f, aabb.BottomLeft, aabb.TopRight);
            if ((farLocal - localHole).LengthSquared() < 0.01f)
                farLocal = aabb.Center;
            massCenter = Vector2.Transform(farLocal, worldMatrix);
            toHole = holePos - massCenter;
            distCenter = toHole.Length();
            if (distCenter < 0.05f)
                return;
        }

        var dir = toHole / distCenter;
        var t = Math.Clamp(1f - distCenter / MathF.Max(pullR, 1f), 0f, 1f);
        var speed = distClosest < horizonR
            ? pullAcceleration * (0.65f + 0.55f * t)
            : pullAcceleration * (0.15f + 0.55f * t * t);

        speed = MathF.Min(speed, distCenter / MathF.Max(dt, 0.05f) * 0.5f);

        _physics.SetLinearVelocity(gridUid, dir * speed, body: physics);

        var ang = physics.AngularVelocity;
        if (ang != 0f)
            _physics.SetAngularVelocity(gridUid, ang * MathF.Max(0f, 1f - 3f * dt), body: physics);
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
