// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using Content.Server.Emp;
using Content.Server.Electrocution;
using Content.Server.Lightning;
using Content.Server.Temperature.Systems;
using Content.Shared._Lua.AmbientSpaceEffects;
using Content.Shared._Lua.SpaceHazards;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Maps;
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
using System.Numerics;

namespace Content.Server._Lua.SpaceHazards;

public sealed class NebulaWeatherSystem : EntitySystem
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);
    private static readonly EntProtoId LightningBoltProto = "Lightning";
    private static readonly EntProtoId LightningSparkProto = "Spark";
    private static readonly EntProtoId EmpBlastProto = "EffectEmpBlastLua";
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

    private TimeSpan _nextTick;
    private readonly List<EntityUid> _gridScratch = new();
    private readonly List<Entity<MapGridComponent>> _gridQueryScratch = new();
    private readonly HashSet<EntityUid> _veiledThisTick = new();
    private readonly List<EntityUid> _activeScratch = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextTick)
            return;

        _nextTick = now + TickInterval;
        _veiledThisTick.Clear();
        _activeScratch.Clear();
        _activeScratch.AddRange(_activity.ActiveHazards);

        foreach (var uid in _activeScratch)
        {
            if (!TryComp(uid, out AmbientSpaceFieldComponent? field))
                continue;

            if (field.Weather is not { } weatherId)
                continue;

            if (!_prototypes.TryIndex(weatherId, out NebulaWeatherPrototype? weather))
                continue;

            if (!TryComp(uid, out TransformComponent? xform) || xform.MapID == MapId.Nullspace)
                continue;

            var fieldPos = _transform.GetWorldPosition(xform);
            var radius = MathF.Max(field.Radius, 1f);
            SyncFieldRadiationSource(uid, field, weather, radius);
            CollectAffectedGrids(xform.MapID, field, fieldPos, radius, _gridScratch);

            foreach (var gridUid in _gridScratch)
            {
                ApplyWeather(gridUid, weather, xform.MapID);
                if (weather.Kind == NebulaWeatherKind.Veil)
                    _veiledThisTick.Add(gridUid);
            }

            ApplyMobWeather(field, fieldPos, radius, weather, xform.MapID);
        }

        CleanupVeil();
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

    private void ApplyWeather(EntityUid gridUid, NebulaWeatherPrototype weather, MapId mapId)
    {
        switch (weather.Kind)
        {
            case NebulaWeatherKind.Veil:
                EnsureVeil(gridUid);
                break;
            case NebulaWeatherKind.EmpStorm:
                MaybeEmpOnHull(gridUid, weather, mapId, effectPrototype: EmpBlastProto);
                MaybeSpawnHullEffect(gridUid, mapId, EmpPulseSpriteProto, chance: 0.55f, maxSpawns: 2);
                MaybeSpawnHullEffect(gridUid, mapId, SparksEffectProto, chance: 0.7f, maxSpawns: 3);
                break;
            case NebulaWeatherKind.Lightning:
                ApplyHullDamageSample(gridUid, weather);
                MaybeEmpOnHull(gridUid, weather, mapId, chanceOverride: weather.EmpChance, effectPrototype: EmpBlastProto);
                SpawnLightningArcs(gridUid, mapId);
                break;
            case NebulaWeatherKind.Corrosion:
                ApplyHullDamageSample(gridUid, weather);
                MaybeSpawnHullEffect(gridUid, mapId, AcidEffectProto, chance: 0.65f, maxSpawns: 3);
                break;
            case NebulaWeatherKind.RadiationFog:
                ApplyHullDamageSample(gridUid, weather);
                MaybeSpawnHullEffect(gridUid, mapId, RadiationEffectProto, chance: 0.55f, maxSpawns: 2);
                break;
            case NebulaWeatherKind.HeatWash:
                ApplyHullDamageSample(gridUid, weather);
                SpawnHeatFlashes(gridUid, mapId);
                MaybeSpawnHullEffect(gridUid, mapId, SparksEffectProto, chance: 0.5f, maxSpawns: 2);
                break;
            default:
                ApplyHullDamageSample(gridUid, weather);
                break;
        }
    }

    private void EnsureVeil(EntityUid gridUid)
    {
        if (!HasComp<NebulaVeilTrackedComponent>(gridUid))
        {
            EnsureComp<NebulaVeilTrackedComponent>(gridUid);
            var stealth = EnsureComp<StealthComponent>(gridUid);
            _stealth.SetEnabled(gridUid, true, stealth);
        }
        else if (TryComp<StealthComponent>(gridUid, out var stealth))
        {
            _stealth.SetEnabled(gridUid, true, stealth);
        }
    }

    private void CleanupVeil()
    {
        var query = EntityQueryEnumerator<NebulaVeilTrackedComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (_veiledThisTick.Contains(uid))
                continue;

            if (TryComp<StealthComponent>(uid, out var stealth))
                _stealth.SetEnabled(uid, false, stealth);

            RemComp<NebulaVeilTrackedComponent>(uid);
            RemCompDeferred<StealthComponent>(uid);
        }
    }

    private void MaybeEmpOnHull(
        EntityUid gridUid,
        NebulaWeatherPrototype weather,
        MapId mapId,
        float? chanceOverride = null,
        string? effectPrototype = null)
    {
        var chance = chanceOverride ?? weather.EmpChance;
        if (chance <= 0f || !_random.Prob(chance))
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
                weather.EmpDuration,
                effectPrototype: effectPrototype);
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
        if (weather.Kind != NebulaWeatherKind.RadiationFog)
        {
            if (TryComp(fieldUid, out RadiationSourceComponent? existing))
                existing.Enabled = false;
            return;
        }

        var mobDamage = weather.MobDamage.Empty ? weather.Damage : weather.MobDamage;
        var peak = SectorCelestialMobDamage.GetDamageAmount(
            mobDamage,
            SectorCelestialMobDamage.RadiationDamageType);
        if (peak <= 0f)
            peak = 6f;

        var source = EnsureComp<RadiationSourceComponent>(fieldUid);
        SectorCelestialMobDamage.SyncRadiationSource(fieldUid, source, radius, peak);
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

        if (!_random.Prob(MathF.Min(weather.DamageChance * 1.25f, 1f)))
            return;

        bool InMid(Vector2 pos) => NebulaVeilHelpers.IsInMidZone(field, fieldPos, pos, radius);
        var heatUnits = SectorCelestialMobDamage.GetDamageAmount(mobDamage, SectorCelestialMobDamage.HeatDamageType);
        if (heatUnits > 0f)
        {
            SectorCelestialMobDamage.HeatMobsWhere(
                mapId,
                fieldPos,
                radius,
                heatUnits * SectorCelestialMobDamage.HeatJoulesPerDamageUnit,
                InMid,
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
                InMid,
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
            InMid,
            _lookup,
            _transform,
            _damageable,
            EntityManager);
    }

    private void SpawnLightningArcs(EntityUid gridUid, MapId mapId)
    {
        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var bolts = _random.Next(2, 5);
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
            if (_random.Prob(0.55f))
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
