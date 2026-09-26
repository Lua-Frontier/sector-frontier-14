// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Collections.Generic;
using System.Numerics;
using Content.Server.Radio;
using Content.Server.Shuttles.Events;
using Content.Lua.Shared.AmbientSpaceEffects;
using Content.Lua.Shared.SpaceHazards;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Lua.Server.SpaceHazards;

public sealed class NebulaEnvironmentSystem : SharedNebulaEnvironmentSystem, INebulaEnvironmentSystem
{
    private const int MaxParentChecks = 8;

    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    private readonly Dictionary<EntityUid, float> _thrustResistance = new();
    private readonly Dictionary<EntityUid, float> _thrustMultiplierCache = new();
    private readonly List<NebulaWeatherPrototype> _weatherScratch = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ConsoleFTLAttemptEvent>(OnFtlAttempt);
        SubscribeLocalEvent<RadioSendAttemptEvent>(OnRadioSendAttempt);
        SubscribeLocalEvent<RadioReceiveAttemptEvent>(OnRadioReceiveAttempt);
        SubscribeLocalEvent<NebulaThrustResistanceComponent, ComponentStartup>(OnThrustResistanceChanged);
        SubscribeLocalEvent<NebulaThrustResistanceComponent, ComponentShutdown>(OnThrustResistanceChanged);
        SubscribeLocalEvent<NebulaThrustResistanceComponent, EntParentChangedMessage>(OnThrustResistanceMoved);
        SubscribeLocalEvent<NebulaPresenceComponent, ComponentShutdown>(OnPresenceShutdown);
    }

    public float GetThrustMultiplier(EntityUid gridUid)
    {
        if (_thrustMultiplierCache.TryGetValue(gridUid, out var cached))
            return cached;

        var multiplier = 1f;
        if (TryFillActiveWeathers(gridUid, _weatherScratch))
        {
            foreach (var weather in _weatherScratch)
                multiplier = MathF.Min(multiplier, weather.ThrustMultiplier);
        }

        if (multiplier < 1f)
        {
            var resistance = GetGridThrustResistance(gridUid);
            multiplier = float.Lerp(multiplier, 1f, resistance);
        }

        _thrustMultiplierCache[gridUid] = multiplier;
        return multiplier;
    }

    public void InvalidateThrustCache(EntityUid gridUid)
        => _thrustMultiplierCache.Remove(gridUid);

    private void OnPresenceShutdown(EntityUid uid, NebulaPresenceComponent component, ComponentShutdown args)
    {
        _thrustMultiplierCache.Remove(uid);
        _thrustResistance.Remove(uid);
    }

    private void OnThrustResistanceChanged(Entity<NebulaThrustResistanceComponent> ent, ref ComponentStartup args)
        => ClearThrustCaches();

    private void OnThrustResistanceChanged(Entity<NebulaThrustResistanceComponent> ent, ref ComponentShutdown args)
        => ClearThrustCaches();

    private void OnThrustResistanceMoved(Entity<NebulaThrustResistanceComponent> ent, ref EntParentChangedMessage args)
        => ClearThrustCaches();

    private void ClearThrustCaches()
    {
        _thrustResistance.Clear();
        _thrustMultiplierCache.Clear();
    }

    private void OnFtlAttempt(ref ConsoleFTLAttemptEvent args)
    {
        var blockedAtOrigin = HasBlockingWeather(args.Uid, static w => w.BlocksFtl);
        var blockedAtDestination = args.Destination is { } destination && IsFtlBlockedAt(destination);
        if (!blockedAtOrigin && !blockedAtDestination)
            return;

        args.Cancelled = true;
        args.Reason = Loc.GetString("nebula-ftl-blocked");
    }

    public bool IsFtlBlockedAt(EntityCoordinates destination)
    {
        var mapCoordinates = _transform.ToMapCoordinates(destination);
        return IsFtlBlockedAt(mapCoordinates.MapId, mapCoordinates.Position);
    }

    public bool IsFtlBlockedAt(MapId mapId, Vector2 worldPosition)
    {
        var query = EntityQueryEnumerator<AmbientSpaceFieldComponent, TransformComponent>();
        while (query.MoveNext(out _, out var field, out var xform))
        {
            if (xform.MapID != mapId || !FieldBlocksFtl(field))
                continue;

            var fieldPosition = _transform.GetWorldPosition(xform);
            var radius = MathF.Max(field.Radius, 1f);
            if ((worldPosition - fieldPosition).LengthSquared() > radius * radius)
                continue;

            if (NebulaVeilHelpers.IsInMidZone(field, fieldPosition, worldPosition, radius))
                return true;
        }

        return false;
    }

    public void CollectFtlBlockingFields(MapId mapId, List<(AmbientSpaceFieldComponent Field, Vector2 Position)> output)
    {
        output.Clear();
        var query = EntityQueryEnumerator<AmbientSpaceFieldComponent, TransformComponent>();
        while (query.MoveNext(out _, out var field, out var xform))
        {
            if (xform.MapID != mapId || !FieldBlocksFtl(field))
                continue;

            output.Add((field, _transform.GetWorldPosition(xform)));
        }
    }

    public static bool IsFtlBlockedByFields(
        Vector2 worldPosition,
        List<(AmbientSpaceFieldComponent Field, Vector2 Position)> fields)
    {
        foreach (var (field, fieldPosition) in fields)
        {
            var radius = MathF.Max(field.Radius, 1f);
            if ((worldPosition - fieldPosition).LengthSquared() > radius * radius)
                continue;

            if (NebulaVeilHelpers.IsInMidZone(field, fieldPosition, worldPosition, radius))
                return true;
        }

        return false;
    }

    private bool FieldBlocksFtl(AmbientSpaceFieldComponent field)
    {
        if (!field.HasWeather)
            return false;

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

    private bool IsRadioBlocked(EntityUid uid)
    {
        if (Deleted(uid) || HasComp<NebulaRadioProtectedComponent>(uid))
            return false;

        var current = uid;
        for (var i = 0; i < MaxParentChecks && current.Valid; i++)
        {
            if (HasComp<NebulaRadioProtectedComponent>(current))
                return false;

            if (HasBlockingWeather(current, static w => w.RadioBlackout))
                return true;

            if (!TryComp(current, out TransformComponent? xform))
                break;

            if (xform.GridUid is { } grid && HasBlockingWeather(grid, static w => w.RadioBlackout))
                return true;

            if (!xform.ParentUid.Valid || xform.ParentUid == current)
                break;

            current = xform.ParentUid;
        }

        return false;
    }

    private bool HasBlockingWeather(EntityUid uid, Func<NebulaWeatherPrototype, bool> predicate)
    {
        if (!TryFillActiveWeathers(uid, _weatherScratch))
            return false;

        foreach (var weather in _weatherScratch)
        {
            if (predicate(weather))
                return true;
        }

        return false;
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
