// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Server.Radio;
using Content.Server.Shuttles.Events;
using Content.Shared._Lua.AmbientSpaceEffects;
using Content.Shared._Lua.SpaceHazards;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Server._Lua.SpaceHazards;

public sealed class NebulaEnvironmentSystem : EntitySystem
{
    private const int MaxParentChecks = 8;

    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SpaceHazardActivitySystem _activity = default!;
    private readonly Dictionary<EntityUid, float> _thrustResistance = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ConsoleFTLAttemptEvent>(OnFtlAttempt);
        SubscribeLocalEvent<RadioSendAttemptEvent>(OnRadioSendAttempt);
        SubscribeLocalEvent<RadioReceiveAttemptEvent>(OnRadioReceiveAttempt);
        SubscribeLocalEvent<GunComponent, QueryFireRateMultiplierEvent>(OnFireRateQuery);
        SubscribeLocalEvent<NebulaThrustResistanceComponent, ComponentStartup>(OnThrustResistanceChanged);
        SubscribeLocalEvent<NebulaThrustResistanceComponent, ComponentShutdown>(OnThrustResistanceChanged);
        SubscribeLocalEvent<NebulaThrustResistanceComponent, EntParentChangedMessage>(OnThrustResistanceMoved);
    }

    public float GetThrustMultiplier(EntityUid gridUid)
    {
        var multiplier = 1f;
        foreach (var weather in GetActiveWeathers(gridUid))
            multiplier = MathF.Min(multiplier, weather.ThrustMultiplier);

        if (multiplier >= 1f)
            return multiplier;

        var resistance = GetGridThrustResistance(gridUid);
        return float.Lerp(multiplier, 1f, resistance);
    }

    private void OnThrustResistanceChanged(Entity<NebulaThrustResistanceComponent> ent, ref ComponentStartup args)
        => _thrustResistance.Clear();

    private void OnThrustResistanceChanged(Entity<NebulaThrustResistanceComponent> ent, ref ComponentShutdown args)
        => _thrustResistance.Clear();

    private void OnThrustResistanceMoved(Entity<NebulaThrustResistanceComponent> ent, ref EntParentChangedMessage args)
        => _thrustResistance.Clear();

    private void OnFtlAttempt(ref ConsoleFTLAttemptEvent args)
    {
        var blockedAtOrigin = GetActiveWeathers(args.Uid).Any(weather => weather.BlocksFtl);
        var blockedAtDestination = args.Destination is { } destination && IsFtlBlockedAt(destination);
        if (!blockedAtOrigin && !blockedAtDestination)
            return;

        args.Cancelled = true;
        args.Reason = Loc.GetString("nebula-ftl-blocked");
    }

    private bool IsFtlBlockedAt(EntityCoordinates destination)
    {
        var mapCoordinates = _transform.ToMapCoordinates(destination);
        foreach (var uid in _activity.ActiveHazards)
        {
            if (!TryComp(uid, out AmbientSpaceFieldComponent? field) ||
                !TryComp(uid, out TransformComponent? xform) ||
                xform.MapID != mapCoordinates.MapId)
            {
                continue;
            }

            var fieldPosition = _transform.GetWorldPosition(xform);
            if (!NebulaVeilHelpers.IsInMidZone(field, fieldPosition, mapCoordinates.Position, field.Radius))
                continue;

            if (FieldBlocksFtl(field))
                return true;
        }

        return false;
    }

    private bool FieldBlocksFtl(AmbientSpaceFieldComponent field)
    {
        if (field.Weathers.Count > 0)
        {
            foreach (var weatherId in field.Weathers)
            {
                if (_prototypes.TryIndex(weatherId, out NebulaWeatherPrototype? weather) && weather.BlocksFtl)
                    return true;
            }

            return false;
        }

        return field.Weather is { } fallbackId &&
               _prototypes.TryIndex(fallbackId, out NebulaWeatherPrototype? fallback) &&
               fallback.BlocksFtl;
    }

    private void OnRadioSendAttempt(ref RadioSendAttemptEvent args)
    {
        if (IsRadioBlocked(args.RadioSource))
            args.Cancelled = true;
    }

    private void OnRadioReceiveAttempt(ref RadioReceiveAttemptEvent args)
    {
        if (IsRadioBlocked(args.RadioSource) || IsRadioBlocked(args.RadioReceiver))
            args.Cancelled = true;
    }

    private void OnFireRateQuery(Entity<GunComponent> ent, ref QueryFireRateMultiplierEvent args)
    {
        var xform = Transform(ent.Owner);
        if (xform.GridUid is not { } gridUid)
            return;

        var cooldownMultiplier = 1f;
        foreach (var weather in GetActiveWeathers(gridUid))
            cooldownMultiplier = MathF.Max(cooldownMultiplier, weather.WeaponCooldownMultiplier);

        if (cooldownMultiplier <= 1f)
            return;

        var resistance = HasComp<NebulaWeaponResistanceComponent>(ent.Owner)
            ? Math.Clamp(Comp<NebulaWeaponResistanceComponent>(ent.Owner).Resistance, 0f, 1f)
            : 0f;
        args.ReloadTimeMul *= float.Lerp(cooldownMultiplier, 1f, resistance);
    }

    private bool IsRadioBlocked(EntityUid uid)
    {
        if (Deleted(uid) || HasComp<NebulaRadioProtectedComponent>(uid))
            return false;

        var current = uid;
        for (var i = 0; i < MaxParentChecks && current.Valid; i++)
        {
            if (HasComp<NebulaRadioProtectedComponent>(current))
                return false;

            if (GetActiveWeathers(current).Any(weather => weather.RadioBlackout))
                return true;

            if (!TryComp(current, out TransformComponent? xform))
                break;

            if (xform.GridUid is { } grid && GetActiveWeathers(grid).Any(weather => weather.RadioBlackout))
                return true;

            if (!xform.ParentUid.Valid || xform.ParentUid == current)
                break;

            current = xform.ParentUid;
        }

        return false;
    }

    private IEnumerable<NebulaWeatherPrototype> GetActiveWeathers(EntityUid uid)
    {
        if (!TryComp(uid, out NebulaPresenceComponent? presence))
            yield break;

        if (presence.ActiveWeathers.Count == 0)
        {
            if (_prototypes.TryIndex(presence.Weather, out NebulaWeatherPrototype? fallback))
                yield return fallback;
            yield break;
        }

        foreach (var weatherId in presence.ActiveWeathers)
        {
            if (_prototypes.TryIndex(weatherId, out NebulaWeatherPrototype? weather))
                yield return weather;
        }
    }

    private float GetGridThrustResistance(EntityUid gridUid)
    {
        if (_thrustResistance.TryGetValue(gridUid, out var cached))
            return cached;

        var resistance = 0f;
        var query = EntityQueryEnumerator<NebulaThrustResistanceComponent, TransformComponent>();
        while (query.MoveNext(out _, out var component, out var xform))
        {
            if (xform.GridUid == gridUid)
                resistance = MathF.Max(resistance, component.Resistance);
        }

        resistance = Math.Clamp(resistance, 0f, 1f);
        _thrustResistance[gridUid] = resistance;
        return resistance;
    }
}
