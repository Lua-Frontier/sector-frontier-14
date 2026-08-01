// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Server.Emp;
using Content.Server.Electrocution;
using Content.Server.Lightning;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Radiation.Components;
using Content.Server.Radiation.Events;
using Content.Server.Temperature.Components;
using Content.Server.Temperature.Systems;
using Content.Shared._Lua.AmbientSpaceEffects;
using Content.Shared._Lua.SpaceHazards;
using Content.Shared.Atmos;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Radiation.Components;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;
using System.Linq;
using System.Numerics;

namespace Content.Server._Lua.SpaceHazards;

public sealed class NebulaWeatherSystem : EntitySystem
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);
    private static readonly EntProtoId LightningBoltProto = "Lightning";
    private static readonly EntProtoId LightningSparkProto = "Spark";
    private static readonly EntProtoId AcidEffectProto = "Acidifier";
    private static readonly EntProtoId RadiationEffectProto = "NebulaRadiationPulse";
    private static readonly EntProtoId SparksEffectProto = "EffectSparks";
    private static readonly EntProtoId EmpPulseSpriteProto = "EffectEmpPulse";
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EmpSystem _emp = default!;
    [Dependency] private readonly SharedStealthSystem _stealth = default!;
    [Dependency] private readonly LightningSystem _lightning = default!;
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SpaceHazardActivitySystem _activity = default!;
    [Dependency] private readonly TemperatureSystem _temperature = default!;
    [Dependency] private readonly ElectrocutionSystem _electrocution = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;

    private TimeSpan _nextTick;
    private readonly List<EntityUid> _gridScratch = new();
    private readonly List<Entity<MapGridComponent>> _gridQueryScratch = new();
    private readonly HashSet<EntityUid> _veiledThisTick = new();
    private readonly HashSet<EntityUid> _presentThisTick = new();
    private readonly Dictionary<EntityUid, int> _presencePriorities = new();
    private readonly Dictionary<EntityUid, Dictionary<ProtoId<NebulaWeatherPrototype>, float>> _weatherSnapshots = new();
    private readonly List<EntityUid> _activeScratch = new();
    private readonly Dictionary<(EntityUid Grid, string Weather), TimeSpan> _nextWeatherEvents = new();
    private readonly HashSet<(EntityUid Grid, string Weather)> _weatherEventsSeenThisTick = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RadiationReceiverComponent, GetAmbientRadiationEvent>(OnGetAmbientRadiation);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextTick)
            return;

        _nextTick = now + TickInterval;
        _veiledThisTick.Clear();
        _presentThisTick.Clear();
        _presencePriorities.Clear();
        _weatherSnapshots.Clear();
        _weatherEventsSeenThisTick.Clear();
        _activeScratch.Clear();
        _activeScratch.AddRange(_activity.ActiveHazards);

        foreach (var uid in _activeScratch)
        {
            if (!TryComp(uid, out AmbientSpaceFieldComponent? field))
                continue;

            if (!TryComp(uid, out TransformComponent? xform) || xform.MapID == MapId.Nullspace)
                continue;

            var fieldPos = _transform.GetWorldPosition(xform);
            var radius = MathF.Max(field.Radius, 1f);
            CollectAffectedGrids(xform.MapID, field, fieldPos, radius, _gridScratch);

            foreach (var weatherId in GetFieldWeatherIds(field))
            {
                if (!_prototypes.TryIndex(weatherId, out NebulaWeatherPrototype? weather))
                    continue;

                SyncFieldRadiationSource(uid, field, weather, radius);
                foreach (var gridUid in _gridScratch)
                {
                    SetPresence(gridUid, field, fieldPos, radius, weatherId, weather);
                    ApplyWeather(gridUid, weatherId, weather, xform.MapID, now);
                    if (weather.Kind == NebulaWeatherKind.Veil)
                        _veiledThisTick.Add(gridUid);
                }

                if (IsWeatherEventDue(uid, weatherId, weather, now))
                    ApplyMobWeather(field, fieldPos, radius, weather, xform.MapID);
            }
        }

        CommitPresenceSnapshots();
        CleanupVeil();
        CleanupPresence();
        CleanupWeatherEventTimers();
    }

    private void CommitPresenceSnapshots()
    {
        foreach (var (gridUid, snapshot) in _weatherSnapshots)
        {
            if (!TryComp(gridUid, out NebulaPresenceComponent? presence))
                continue;

            presence.ActiveWeathers.Clear();
            presence.ActiveIntensities.Clear();
            foreach (var (weatherId, intensity) in snapshot)
            {
                presence.ActiveWeathers.Add(weatherId);
                presence.ActiveIntensities.Add(intensity);
            }

            Dirty(gridUid, presence);
        }
    }

    private static IEnumerable<ProtoId<NebulaWeatherPrototype>> GetFieldWeatherIds(
        AmbientSpaceFieldComponent field)
    {
        if (field.Weathers.Count > 0)
        {
            foreach (var weatherId in field.Weathers)
                yield return weatherId;
            yield break;
        }

        if (field.Weather is { } fallback)
            yield return fallback;
    }

    private void OnGetAmbientRadiation(
        Entity<RadiationReceiverComponent> ent,
        ref GetAmbientRadiationEvent args)
    {
        if (!TryComp(ent.Owner, out TransformComponent? receiverXform) ||
            receiverXform.MapID == MapId.Nullspace ||
            !IsMobExposedToNebula(ent.Owner))
            return;

        var receiverPosition = _transform.GetWorldPosition(receiverXform);
        var strongestRadiation = 0f;
        var fields = EntityQueryEnumerator<AmbientSpaceFieldComponent, TransformComponent>();
        while (fields.MoveNext(out _, out var field, out var fieldXform))
        {
            if (fieldXform.MapID != receiverXform.MapID)
                continue;

            var fieldPosition = _transform.GetWorldPosition(fieldXform);
            if (!NebulaVeilHelpers.IsInMidZone(field, fieldPosition, receiverPosition, field.Radius))
                continue;

            foreach (var weatherId in GetFieldWeatherIds(field))
            {
                if (!_prototypes.TryIndex(weatherId, out NebulaWeatherPrototype? weather) ||
                    weather.Kind != NebulaWeatherKind.RadiationFog ||
                    weather.RadiationIntensity <= 0f)
                    continue;

                var normalized = (receiverPosition - fieldPosition) / MathF.Max(field.Radius, 1f);
                var intensity = AmbientSpaceNebulaNoise.SamplePresence(normalized, field.Seed, field.Density, 1f, field.Radius);
                strongestRadiation = MathF.Max(strongestRadiation, weather.RadiationIntensity * Math.Clamp(intensity, 0.25f, 1f));
            }
        }

        args.Radiation += strongestRadiation;
    }

    private void SetPresence(
        EntityUid gridUid,
        AmbientSpaceFieldComponent field,
        Vector2 fieldPos,
        float radius,
        ProtoId<NebulaWeatherPrototype> weatherId,
        NebulaWeatherPrototype weather)
    {
        _presentThisTick.Add(gridUid);
        var presence = EnsureComp<NebulaPresenceComponent>(gridUid);

        var samplePos = TryComp<MapGridComponent>(gridUid, out var grid)
            ? ClosestPointOnGrid(gridUid, grid, fieldPos)
            : _transform.GetWorldPosition(gridUid);
        var normalized = (samplePos - fieldPos) / radius;
        var intensity = AmbientSpaceNebulaNoise.SamplePresence(normalized, field.Seed, field.Density, 1f, radius);

        if (!_weatherSnapshots.TryGetValue(gridUid, out var snapshot))
        {
            snapshot = new Dictionary<ProtoId<NebulaWeatherPrototype>, float>();
            _weatherSnapshots.Add(gridUid, snapshot);
        }

        if (!snapshot.TryGetValue(weatherId, out var existingIntensity) || intensity > existingIntensity)
            snapshot[weatherId] = intensity;

        if (_presencePriorities.TryGetValue(gridUid, out var currentPriority) && currentPriority > weather.Priority)
            return;

        _presencePriorities[gridUid] = weather.Priority;
        if (presence.Weather == weatherId && MathF.Abs(presence.Intensity - intensity) < 0.01f)
            return;

        presence.Weather = weatherId;
        presence.Intensity = intensity;
        Dirty(gridUid, presence);
    }

    private void CleanupPresence()
    {
        var query = EntityQueryEnumerator<NebulaPresenceComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!_presentThisTick.Contains(uid))
                RemCompDeferred<NebulaPresenceComponent>(uid);
        }
    }

    private void CollectAffectedGrids(
        MapId mapId,
        AmbientSpaceFieldComponent field,
        Vector2 fieldPos,
        float radius,
        List<EntityUid> output)
    {
        output.Clear();
        var grids = _gridQueryScratch;
        grids.Clear();
        var box = Box2.CenteredAround(fieldPos, new Vector2(radius * 2f, radius * 2f));
        _mapManager.FindGridsIntersecting(mapId, box, ref grids, approx: true, includeMap: false);
        foreach (var grid in grids)
        {
            var samplePos = ClosestPointOnGrid(grid.Owner, grid.Comp, fieldPos);
            if (!NebulaVeilHelpers.IsInMidZone(field, fieldPos, samplePos, radius))
                continue;

            output.Add(grid.Owner);
        }
    }

    private Vector2 ClosestPointOnGrid(EntityUid gridUid, MapGridComponent grid, Vector2 worldPoint)
    {
        var inv = _transform.GetInvWorldMatrix(gridUid);
        var local = Vector2.Transform(worldPoint, inv);
        var aabb = grid.LocalAABB;
        var closest = Vector2.Clamp(local, aabb.BottomLeft, aabb.TopRight);
        return Vector2.Transform(closest, _transform.GetWorldMatrix(gridUid));
    }

    public static bool IsInMidZone(AmbientSpaceFieldComponent field, Vector2 fieldPos, Vector2 worldPos, float? radiusOverride = null)
        => NebulaVeilHelpers.IsInMidZone(field, fieldPos, worldPos, radiusOverride);

    private void ApplyWeather(
        EntityUid gridUid,
        ProtoId<NebulaWeatherPrototype> weatherId,
        NebulaWeatherPrototype weather,
        MapId mapId,
        TimeSpan now)
    {
        if (weather.Kind == NebulaWeatherKind.Veil)
        {
            EnsureVeil(gridUid);
            return;
        }

        if (!IsWeatherEventDue(gridUid, weatherId, weather, now))
            return;

        switch (weather.Kind)
        {
            case NebulaWeatherKind.EmpStorm:
                EmpOnHull(gridUid, weather, mapId);
                MaybeSpawnHullEffect(gridUid, mapId, EmpPulseSpriteProto, chance: 0.5f, maxSpawns: 1);
                break;
            case NebulaWeatherKind.Lightning:
                if (TryAbsorbGridHazard(gridUid, weather))
                    break;
                ApplyHullDamageSample(gridUid, weather);
                MaybeEmpOnHull(gridUid, weather, mapId);
                SpawnLightningArcs(gridUid, mapId);
                break;
            case NebulaWeatherKind.Corrosion:
                if (TryAbsorbGridHazard(gridUid, weather))
                    break;
                ApplyHullDamageSample(gridUid, weather);
                MaybeSpawnHullEffect(gridUid, mapId, AcidEffectProto, chance: 0.65f, maxSpawns: 3);
                break;
            case NebulaWeatherKind.RadiationFog:
                if (TryAbsorbGridHazard(gridUid, weather))
                    break;
                ApplyHullDamageSample(gridUid, weather);
                MaybeSpawnHullEffect(gridUid, mapId, RadiationEffectProto, chance: 0.55f, maxSpawns: 2);
                break;
            case NebulaWeatherKind.HeatWash:
                if (TryAbsorbGridHazard(gridUid, weather))
                    break;
                ApplyHullDamageSample(gridUid, weather);
                SpawnHeatFlashes(gridUid, mapId);
                MaybeSpawnHullEffect(gridUid, mapId, SparksEffectProto, chance: 0.5f, maxSpawns: 2);
                break;
            default:
                ApplyHullDamageSample(gridUid, weather);
                break;
        }
    }

    private bool IsWeatherEventDue(
        EntityUid gridUid,
        ProtoId<NebulaWeatherPrototype> weatherId,
        NebulaWeatherPrototype weather,
        TimeSpan now)
    {
        var key = (gridUid, weatherId.Id);
        _weatherEventsSeenThisTick.Add(key);

        if (_nextWeatherEvents.TryGetValue(key, out var nextEvent) && now < nextEvent)
            return false;

        var minDelay = Math.Max(1, Math.Min(weather.MinEventDelaySeconds, weather.MaxEventDelaySeconds));
        var maxDelay = Math.Max(minDelay, Math.Max(weather.MinEventDelaySeconds, weather.MaxEventDelaySeconds));
        _nextWeatherEvents[key] = now + TimeSpan.FromSeconds(_random.Next(minDelay, maxDelay + 1));
        return nextEvent != default;
    }

    private void CleanupWeatherEventTimers()
    {
        foreach (var key in _nextWeatherEvents.Keys.ToArray())
        {
            if (!_weatherEventsSeenThisTick.Contains(key))
                _nextWeatherEvents.Remove(key);
        }
    }

    private bool TryAbsorbGridHazard(EntityUid gridUid, NebulaWeatherPrototype weather)
    {
        if (weather.ShieldLoad <= 0f || !_random.Prob(weather.DamageChance))
            return false;

        var attempt = new NebulaShieldHitAttemptEvent(weather.ShieldLoad);
        RaiseLocalEvent(gridUid, ref attempt);
        return attempt.Absorbed;
    }

    private void EnsureVeil(EntityUid gridUid)
    {
        if (!TryComp<NebulaVeilTrackedComponent>(gridUid, out var tracked))
        {
            tracked = EnsureComp<NebulaVeilTrackedComponent>(gridUid);
            tracked.AddedStealth = !TryComp<StealthComponent>(gridUid, out var existingStealth);
            if (existingStealth != null)
            {
                tracked.PreviousEnabled = existingStealth.Enabled;
                tracked.PreviousVisibility = _stealth.GetVisibility(gridUid, existingStealth);
            }

            var stealth = EnsureComp<StealthComponent>(gridUid);
            _stealth.SetEnabled(gridUid, true, stealth);
            _stealth.SetVisibility(gridUid, stealth.MinVisibility, stealth);
        }
        else if (TryComp<StealthComponent>(gridUid, out var stealth))
        {
            _stealth.SetEnabled(gridUid, true, stealth);
            _stealth.SetVisibility(gridUid, stealth.MinVisibility, stealth);
        }
    }

    private void CleanupVeil()
    {
        var query = EntityQueryEnumerator<NebulaVeilTrackedComponent>();
        while (query.MoveNext(out var uid, out var tracked))
        {
            if (_veiledThisTick.Contains(uid))
                continue;

            if (TryComp<StealthComponent>(uid, out var stealth))
            {
                if (tracked.AddedStealth)
                    _stealth.SetEnabled(uid, false, stealth);
                else
                {
                    _stealth.SetVisibility(uid, tracked.PreviousVisibility, stealth);
                    _stealth.SetEnabled(uid, tracked.PreviousEnabled, stealth);
                }
            }

            RemComp<NebulaVeilTrackedComponent>(uid);
            if (tracked.AddedStealth)
                RemCompDeferred<StealthComponent>(uid);
        }
    }

    private void EmpOnHull(EntityUid gridUid, NebulaWeatherPrototype weather, MapId mapId)
        => MaybeEmpOnHull(gridUid, weather, mapId, force: true);

    private void MaybeEmpOnHull(
        EntityUid gridUid,
        NebulaWeatherPrototype weather,
        MapId mapId,
        bool force = false)
    {
        if (!force && (weather.EmpChance <= 0f || !_random.Prob(weather.EmpChance)))
            return;

        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var pulses = Math.Max(weather.EmpPulsesPerTick, 1);
        var usedTiles = new HashSet<Vector2i>();

        for (var i = 0; i < pulses; i++)
        {
            if (!GridHullExteriorHelper.TryPickRandomExteriorTile(gridUid, grid, _maps, _turf, _random, out var tile))
                break;

            if (usedTiles.Contains(tile))
                continue;

            usedTiles.Add(tile);
            var worldPos = GridHullExteriorHelper.TileCenterWorld(gridUid, grid, _maps, tile);
            _emp.EmpPulse(
                new MapCoordinates(worldPos, mapId),
                weather.EmpRange,
                weather.EmpEnergy,
                weather.EmpDuration);
            Spawn(SparksEffectProto, new MapCoordinates(worldPos, mapId));
        }
    }

    private void ApplyHullDamageSample(EntityUid gridUid, NebulaWeatherPrototype weather)
    {
        if (weather.Damage.Empty)
            return;

        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var damaged = 0;
        var attempts = weather.MaxDamagedPerTick * 6;

        for (var i = 0; i < attempts && damaged < weather.MaxDamagedPerTick; i++)
        {
            if (!_random.Prob(weather.DamageChance))
                continue;

            if (SectorCelestialHullDamage.TryDamageRandomOnGrid(
                    gridUid,
                    grid,
                    weather.Damage,
                    _maps,
                    _turf,
                    _damageable,
                    EntityManager,
                    _random))
            {
                damaged++;
            }
        }
    }

    private void SyncFieldRadiationSource(
        EntityUid fieldUid,
        AmbientSpaceFieldComponent field,
        NebulaWeatherPrototype weather,
        float radius)
    {
        if (TryComp(fieldUid, out RadiationSourceComponent? existing))
            existing.Enabled = false;
    }

    private void ApplyMobWeather(
        AmbientSpaceFieldComponent field,
        Vector2 fieldPos,
        float radius,
        NebulaWeatherPrototype weather,
        MapId mapId)
    {
        if (weather.Kind is NebulaWeatherKind.Veil or NebulaWeatherKind.EmpStorm)
            return;

        var mobDamage = weather.MobDamage.Empty ? weather.Damage : weather.MobDamage;
        if (mobDamage.Empty)
            return;

        bool InMidAndExposed(EntityUid uid, Vector2 pos) =>
            NebulaVeilHelpers.IsInMidZone(field, fieldPos, pos, radius) && IsMobExposedToNebula(uid);
        var heatUnits = SectorCelestialMobDamage.GetDamageAmount(mobDamage, SectorCelestialMobDamage.HeatDamageType);
        if (weather.MobTemperatureIncrease > 0f)
        {
            HeatMobsByTemperatureIncrease(
                mapId,
                fieldPos,
                radius,
                weather.MobTemperatureIncrease,
                InMidAndExposed);
        }
        else if (heatUnits > 0f)
        {
            SectorCelestialMobDamage.HeatMobsWhere(
                mapId,
                fieldPos,
                radius,
                heatUnits * SectorCelestialMobDamage.HeatJoulesPerDamageUnit,
                InMidAndExposed,
                _lookup,
                _transform,
                _temperature,
                EntityManager);
        }

        var shock = SectorCelestialMobDamage.GetDamageAmount(mobDamage, SectorCelestialMobDamage.ShockDamageType);
        if (shock > 0f)
        {
            SectorCelestialMobDamage.ElectrocuteMobsWhere(
                mapId,
                fieldPos,
                radius,
                Math.Max(1, (int) MathF.Round(shock)),
                InMidAndExposed,
                _lookup,
                _transform,
                _electrocution,
                EntityManager);
        }
        SectorCelestialMobDamage.DamageMobsWhere(
            mapId,
            fieldPos,
            radius,
            mobDamage,
            InMidAndExposed,
            _lookup,
            _transform,
            _damageable,
            EntityManager);
    }

    private void HeatMobsByTemperatureIncrease(
        MapId mapId,
        Vector2 fieldPosition,
        float radius,
        float temperatureIncrease,
        Func<EntityUid, Vector2, bool> include)
    {
        var mobs = new HashSet<Entity<MobStateComponent>>();
        _lookup.GetEntitiesInRange(mapId, fieldPosition, radius, mobs, LookupFlags.Dynamic | LookupFlags.Sundries);

        foreach (var (uid, mobState) in mobs)
        {
            if (mobState.CurrentState == MobState.Dead ||
                !TryComp(uid, out TemperatureComponent? temperature) ||
                !include(uid, _transform.GetWorldPosition(uid)))
                continue;

            var heat = _temperature.GetHeatCapacity(uid, temperature) * temperatureIncrease;
            _temperature.ChangeHeat(uid, heat, temperature: temperature);
        }
    }

    private bool IsMobExposedToNebula(EntityUid uid)
    {
        if (!TryComp(uid, out TransformComponent? xform))
            return true;

        if (xform.GridUid is not { } gridUid)
            return true;

        if (!TryComp(gridUid, out MapGridComponent? grid))
            return true;

        var air = _atmosphere.GetContainingMixture((uid, xform));
        if (air != null && air.Pressure >= Atmospherics.OneAtmosphere * 0.2f)
            return false;

        if (!_maps.TryGetTileRef(gridUid, grid, xform.Coordinates, out var tileRef))
            return true;

        if (tileRef.Tile.IsEmpty || _turf.IsSpace(tileRef))
            return true;

        return true;
    }

    private void SpawnLightningArcs(EntityUid gridUid, MapId mapId)
    {
        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var bolts = _random.Next(1, 3);
        for (var i = 0; i < bolts; i++)
        {
            if (!TryPickDistantExteriorPair(gridUid, grid, out var tileA, out var tileB))
                break;

            var posA = GridHullExteriorHelper.TileCenterWorld(gridUid, grid, _maps, tileA);
            var posB = GridHullExteriorHelper.TileCenterWorld(gridUid, grid, _maps, tileB);
            var source = SpawnTempAnchor(new MapCoordinates(posA, mapId));
            var target = SpawnTempAnchor(new MapCoordinates(posB, mapId));

            var proto = _random.Prob(0.35f) ? LightningSparkProto : LightningBoltProto;
            _lightning.ShootLightning(source, target, proto, triggerLightningEvents: false);
            if (_random.Prob(0.25f))
            {
                _lightning.ShootRandomLightnings(
                    source,
                    range: 8f,
                    boltCount: 1,
                    lightningPrototype: LightningBoltProto,
                    arcDepth: 1,
                    triggerLightningEvents: false);
            }

            Spawn(SparksEffectProto, new MapCoordinates(posA, mapId));
            Spawn(SparksEffectProto, new MapCoordinates(posB, mapId));
        }
    }

    private void SpawnHeatFlashes(EntityUid gridUid, MapId mapId)
    {
        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var flashes = _random.Next(1, 4);
        for (var i = 0; i < flashes; i++)
        {
            if (!GridHullExteriorHelper.TryPickRandomExteriorTile(gridUid, grid, _maps, _turf, _random, out var tile))
                break;

            var worldPos = GridHullExteriorHelper.TileCenterWorld(gridUid, grid, _maps, tile);
            var flash = Spawn(null, new MapCoordinates(worldPos, mapId));
            var light = EnsureComp<PointLightComponent>(flash);
            _pointLight.SetColor(flash, Color.FromHex("#FF6A20"), light);
            _pointLight.SetRadius(flash, 7f, light);
            _pointLight.SetEnergy(flash, 10f, light);
            _pointLight.SetCastShadows(flash, false, light);
            EnsureComp<TimedDespawnComponent>(flash).Lifetime = 0.4f;
        }
    }

    private void MaybeSpawnHullEffect(
        EntityUid gridUid,
        MapId mapId,
        EntProtoId effectProto,
        float chance,
        int maxSpawns)
    {
        if (chance <= 0f || maxSpawns <= 0 || !_random.Prob(chance))
            return;

        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var spawned = 0;
        for (var i = 0; i < maxSpawns * 4 && spawned < maxSpawns; i++)
        {
            if (!GridHullExteriorHelper.TryPickRandomExteriorTile(gridUid, grid, _maps, _turf, _random, out var tile))
                break;

            var worldPos = GridHullExteriorHelper.TileCenterWorld(gridUid, grid, _maps, tile);
            Spawn(effectProto, new MapCoordinates(worldPos, mapId));
            spawned++;
        }
    }

    private bool TryPickDistantExteriorPair(
        EntityUid gridUid,
        MapGridComponent grid,
        out Vector2i tileA,
        out Vector2i tileB)
    {
        tileA = default;
        tileB = default;

        for (var i = 0; i < 24; i++)
        {
            if (!GridHullExteriorHelper.TryPickRandomExteriorTile(gridUid, grid, _maps, _turf, _random, out tileA))
                return false;

            if (!GridHullExteriorHelper.TryPickRandomExteriorTile(gridUid, grid, _maps, _turf, _random, out tileB))
                return false;

            var delta = tileA - tileB;
            if (delta.X * delta.X + delta.Y * delta.Y >= 4)
                return true;
        }

        return false;
    }

    private EntityUid SpawnTempAnchor(MapCoordinates coords)
    {
        var uid = Spawn(null, coords);
        EnsureComp<TimedDespawnComponent>(uid).Lifetime = 0.6f;
        return uid;
    }
}
