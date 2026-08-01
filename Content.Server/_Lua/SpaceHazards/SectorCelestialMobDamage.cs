// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Server.Electrocution;
using Content.Server.Temperature.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Radiation.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using System.Numerics;

namespace Content.Server._Lua.SpaceHazards;

public static class SectorCelestialMobDamage
{
    private static readonly LookupFlags MobLookupFlags =
        LookupFlags.Dynamic | LookupFlags.Approximate | LookupFlags.Sundries;

    private static readonly HashSet<Entity<MobStateComponent>> MobScratch = new();

    public const string HeatDamageType = "Heat";
    public const string RadiationDamageType = "Radiation";
    public const string ShockDamageType = "Shock";

    public const float HeatJoulesPerDamageUnit = 4000f;

    private static readonly TimeSpan DefaultShockStun = TimeSpan.FromSeconds(2.5);
    public static void SyncRadiationSource(
        EntityUid uid,
        RadiationSourceComponent source,
        float radiationRange,
        float peakRadsPerSecond)
    {
        var peak = MathF.Max(peakRadsPerSecond, 0.5f);
        var range = MathF.Max(radiationRange, 1f);
        source.Intensity = peak;
        source.Slope = MathF.Max((peak - 0.1f) / range, 0.0005f);
        source.Enabled = true;
    }

    public static float GetDamageAmount(DamageSpecifier damage, string typeId)
    {
        return damage.DamageDict.TryGetValue(typeId, out var amount)
            ? (float) amount
            : 0f;
    }
    public static DamageSpecifier? WithoutPipelineDamage(DamageSpecifier damage)
    {
        if (damage.Empty)
            return null;

        var copy = new DamageSpecifier(damage);
        copy.DamageDict.Remove(HeatDamageType);
        copy.DamageDict.Remove(RadiationDamageType);
        copy.DamageDict.Remove(ShockDamageType);
        var keys = new List<string>();
        foreach (var (key, val) in copy.DamageDict)
        {
            if (val <= FixedPoint2.Zero)
                keys.Add(key);
        }

        foreach (var key in keys)
            copy.DamageDict.Remove(key);

        return copy.Empty ? null : copy;
    }
    public static void ApplyHazardToMobsInRadius(
        MapId mapId,
        Vector2 worldCenter,
        float radius,
        DamageSpecifier hazardDamage,
        EntityLookupSystem lookup,
        SharedTransformSystem transform,
        DamageableSystem damageable,
        TemperatureSystem temperature,
        IEntityManager entMan,
        ElectrocutionSystem? electrocution = null)
    {
        if (radius <= 0f || mapId == MapId.Nullspace)
            return;

        var heatPerUnit = GetDamageAmount(hazardDamage, HeatDamageType);
        var heatJoulesCenter = heatPerUnit * HeatJoulesPerDamageUnit;
        var shock = GetDamageAmount(hazardDamage, ShockDamageType);
        var residual = WithoutPipelineDamage(hazardDamage);

        if (heatJoulesCenter <= 0f && shock <= 0f && residual == null)
            return;

        MobScratch.Clear();
        lookup.GetEntitiesInRange(mapId, worldCenter, radius, MobScratch, MobLookupFlags);

        foreach (var (uid, mobState) in MobScratch)
        {
            if (!IsLivingTarget(entMan, uid, mobState))
                continue;

            var pos = transform.GetWorldPosition(uid);
            var dist = (pos - worldCenter).Length();
            var factor = SectorCelestialProximity.Factor(dist, radius);
            if (factor <= 0f)
                continue;

            if (heatJoulesCenter > 0f)
                temperature.ChangeHeat(uid, heatJoulesCenter * factor);

            if (shock > 0f && electrocution != null)
            {
                var dmg = Math.Max(1, (int) MathF.Round(shock * factor));
                electrocution.TryDoElectrocution(uid, null, dmg, DefaultShockStun, refresh: true);
            }
            if (residual != null)
                damageable.TryChangeDamage(uid, residual * factor, ignoreResistances: false);
        }
    }

    public static void HeatMobsWhere(
        MapId mapId,
        Vector2 worldCenter,
        float scanRadius,
        float heatJoules,
        Func<EntityUid, Vector2, bool> include,
        EntityLookupSystem lookup,
        SharedTransformSystem transform,
        TemperatureSystem temperature,
        IEntityManager entMan)
    {
        if (heatJoules <= 0f || scanRadius <= 0f || mapId == MapId.Nullspace)
            return;

        MobScratch.Clear();
        lookup.GetEntitiesInRange(mapId, worldCenter, scanRadius, MobScratch, MobLookupFlags);

        foreach (var (uid, mobState) in MobScratch)
        {
            if (!IsLivingTarget(entMan, uid, mobState))
                continue;

            var pos = transform.GetWorldPosition(uid);
            if (!include(uid, pos))
                continue;

            temperature.ChangeHeat(uid, heatJoules);
        }
    }

    public static void ElectrocuteMobsWhere(
        MapId mapId,
        Vector2 worldCenter,
        float scanRadius,
        int shockDamage,
        Func<EntityUid, Vector2, bool> include,
        EntityLookupSystem lookup,
        SharedTransformSystem transform,
        ElectrocutionSystem electrocution,
        IEntityManager entMan,
        TimeSpan? stunTime = null)
    {
        if (shockDamage <= 0 || scanRadius <= 0f || mapId == MapId.Nullspace)
            return;

        var stun = stunTime ?? DefaultShockStun;
        MobScratch.Clear();
        lookup.GetEntitiesInRange(mapId, worldCenter, scanRadius, MobScratch, MobLookupFlags);

        foreach (var (uid, mobState) in MobScratch)
        {
            if (!IsLivingTarget(entMan, uid, mobState))
                continue;

            var pos = transform.GetWorldPosition(uid);
            if (!include(uid, pos))
                continue;

            electrocution.TryDoElectrocution(uid, null, shockDamage, stun, refresh: true);
        }
    }

    public static void DamageMobsWhere(
        MapId mapId,
        Vector2 worldCenter,
        float scanRadius,
        DamageSpecifier damage,
        Func<EntityUid, Vector2, bool> include,
        EntityLookupSystem lookup,
        SharedTransformSystem transform,
        DamageableSystem damageable,
        IEntityManager entMan)
    {
        var residual = WithoutPipelineDamage(damage);
        if (residual == null || scanRadius <= 0f || mapId == MapId.Nullspace)
            return;

        MobScratch.Clear();
        lookup.GetEntitiesInRange(mapId, worldCenter, scanRadius, MobScratch, MobLookupFlags);

        foreach (var (uid, mobState) in MobScratch)
        {
            if (!IsLivingTarget(entMan, uid, mobState))
                continue;

            var pos = transform.GetWorldPosition(uid);
            if (!include(uid, pos))
                continue;

            damageable.TryChangeDamage(uid, residual, ignoreResistances: false);
        }
    }

    public static void PullUncontainedMobs(
        MapId mapId,
        Vector2 toward,
        float pullRadius,
        float pullAcceleration,
        float dt,
        EntityLookupSystem lookup,
        SharedTransformSystem transform,
        SharedPhysicsSystem physics,
        IEntityManager entMan)
    {
        if (pullRadius <= 0f || pullAcceleration <= 0f || mapId == MapId.Nullspace)
            return;

        MobScratch.Clear();
        lookup.GetEntitiesInRange(mapId, toward, pullRadius, MobScratch, LookupFlags.Dynamic | LookupFlags.Approximate);

        foreach (var (uid, mobState) in MobScratch)
        {
            if (!IsLivingTarget(entMan, uid, mobState))
                continue;

            if (!entMan.TryGetComponent(uid, out PhysicsComponent? body) || body.BodyType == BodyType.Static)
                continue;

            var xform = entMan.GetComponent<TransformComponent>(uid);
            if (xform.GridUid != null)
                continue;

            var pos = transform.GetWorldPosition(uid);
            var toHole = toward - pos;
            var dist = toHole.Length();
            if (dist > pullRadius)
                continue;

            var vel = body.LinearVelocity;

            if (dist < 0.05f)
            {
                vel *= 0.35f;
            }
            else
            {
                var dir = toHole / dist;
                var t = Math.Clamp(1f - dist / pullRadius, 0f, 1f);
                var strength = pullAcceleration * (0.2f * t + 0.8f * t * t);
                vel += dir * strength * dt;

                var outward = Vector2.Dot(vel, -dir);
                if (outward > 0f)
                    vel += dir * outward;
            }

            physics.SetLinearVelocity(uid, vel, body: body);
        }
    }

    private static bool IsLivingTarget(IEntityManager entMan, EntityUid uid, MobStateComponent mobState)
    {
        if (mobState.CurrentState is MobState.Dead)
            return false;

        if (entMan.HasComponent<GhostComponent>(uid))
            return false;

        return entMan.HasComponent<DamageableComponent>(uid);
    }
}
