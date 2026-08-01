// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Server.Station.Components;
using Content.Server._Lua.SpaceHazards;
using Content.Server._Mono.NPC.HTN;
using Content.Shared._Lua.AmbientSpaceEffects;
using Content.Shared._Lua.Shuttles.Components;
using Content.Shared._Lua.SpaceHazards;
using Content.Shared._Mono.Radar;
using Content.Shared.Lua.CLVar;
using Robust.Server.GameStates;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Lua.AmbientSpaceEffects;

public sealed class AmbientSpaceFieldPlacerSystem : EntitySystem
{
    private const float WeatherSpawnChance = 0.24f;
    private const float WeatherOffPaletteChance = 0.18f;
    private const float RadiationFogMinRadius = 980f;
    private const float RadiationFogFullWeightRadius = 1500f;
    private static readonly ProtoId<NebulaWeatherPrototype> RadiationFogWeather = "NebulaRadiationFog";

    private static readonly ProtoId<NebulaWeatherPrototype>[] WeatherByColorIndex =
    [
        "NebulaLightning",
        "NebulaCorrosion",
        "NebulaEmpStorm",
        "NebulaVeil",
        "NebulaRadiationFog",
        "NebulaHeatWash",
        "NebulaHeatWash",
    ];

    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly PvsOverrideSystem _pvs = default!;
    [Dependency] private readonly SectorLandmarkAnchorSystem _landmarks = default!;
    [Dependency] private readonly ShipSteeringSystem _shipSteering = default!;

    private bool _enabled = true;

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_cfg, CLVars.AmbientSpaceEffectsEnabled, v => _enabled = v, true);
        SubscribeLocalEvent<AmbientSpaceFieldPlacerControllerComponent, ComponentStartup>(OnPlacerStartup);
        SubscribeLocalEvent<AmbientSpaceFieldComponent, ComponentStartup>(OnFieldStartup);
    }

    public void InitializePlacer(EntityUid mapUid)
    {
        if (!_enabled || !TryComp(mapUid, out AmbientSpaceFieldPlacerControllerComponent? placer))
            return;

        TryPregenerate(mapUid, placer);
    }

    private void OnPlacerStartup(EntityUid uid, AmbientSpaceFieldPlacerControllerComponent component, ComponentStartup args)
    {
        TryPregenerate(uid, component);
    }

    private void OnFieldStartup(EntityUid uid, AmbientSpaceFieldComponent component, ComponentStartup args)
    {
        _pvs.AddGlobalOverride(uid);
        _landmarks.LockToMap(uid);
    }

    private void TryPregenerate(EntityUid mapUid, AmbientSpaceFieldPlacerControllerComponent placer)
    {
        if (!_enabled || placer.Spawned)
            return;

        if (!TryComp<MapComponent>(mapUid, out var map))
            return;

        if (!_prototypes.HasIndex<EntityPrototype>(placer.FieldPrototype))
        {
            Log.Error($"Ambient space field prototype '{placer.FieldPrototype}' missing");
            placer.Spawned = true;
            return;
        }

        var mapId = map.MapId;
        var count = _random.Next(placer.MinCount, placer.MaxCount + 1);
        var minDist = MathF.Max(0f, placer.MinDistance);
        var maxDist = MathF.Max(minDist + 1f, placer.MaxDistance);
        var minDistSq = minDist * minDist;
        var maxDistSq = maxDist * maxDist;
        var spawned = 0;
        var maxAttempts = Math.Max(count * 24, count + 256);
        var placed = new List<(Vector2 Pos, float Radius)>(count);

        for (var attempt = 0; attempt < maxAttempts && spawned < count; attempt++)
        {
            var seed = _random.Next();
            if (seed == 0)
                seed = 1;

            var radius = AmbientSpacePalette.RadiusFromSeed(seed);
            var angle = _random.NextFloat() * MathF.Tau;
            var dist = MathF.Sqrt(minDistSq + _random.NextFloat() * (maxDistSq - minDistSq));
            var pos = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;

            if (OverlapsPlaced(placed, pos, radius, placer.MinSpacingFactor, placer.MinCenterSeparation))
                continue;

            if (OverlapsStation(mapId, pos, radius + placer.MinStationClearance))
                continue;

            var ent = Spawn(placer.FieldPrototype, new MapCoordinates(pos, mapId));
            if (!TryComp<AmbientSpaceFieldComponent>(ent, out var field))
            {
                QueueDel(ent);
                continue;
            }

            ApplyDeterministicVisuals(field, seed, radius);
            Dirty(ent, field);
            if (field.HasWeather)
                EnsureWeatherRadarBlip(ent, field);
            _pvs.AddGlobalOverride(ent);
            _landmarks.LockToMap(ent);
            placed.Add((pos, radius));
            spawned++;
        }

        _shipSteering.InvalidateHazardCache(mapId);
        placer.Spawned = true;
        Log.Info($"Pregenerated {spawned}/{count} ambient nebula fields on {ToPrettyString(mapUid)} (annulus {minDist}–{maxDist})");
    }

    private void EnsureWeatherRadarBlip(EntityUid uid, AmbientSpaceFieldComponent field)
    {
        var color = Color.FromHex("#C084FC");
        var highlight = Color.FromHex("#E0B0FF");
        var iconPath = new ResPath("/Textures/_Lua/Interface/Radar/nebula_hazard.png");
        ResPath? secondaryPath = null;
        var weatherId = field.Weathers.Count > 0 ? field.Weathers[0] : field.Weather!.Value;

        if (_prototypes.TryIndex(weatherId, out NebulaWeatherPrototype? weather))
        {
            if (weather.RadarIcon is { } actualIcon)
                iconPath = actualIcon;

            if (field.Seed != 0)
            {
                var seedIdx = AmbientSpacePalette.ColorIndexFromSeed(field.Seed);
                if (seedIdx >= 0 && seedIdx < AmbientSpacePalette.Colors.Length)
                {
                    color = AmbientSpacePalette.Colors[seedIdx];
                    highlight = Color.InterpolateBetween(color, Color.White, 0.35f);
                }

                if (field.Weathers.Count > 1
                    && _prototypes.TryIndex(field.Weathers[1], out NebulaWeatherPrototype? secondaryWeather)
                    && secondaryWeather.RadarIcon is { } secondaryIcon
                    && secondaryIcon != iconPath)
                {
                    secondaryPath = secondaryIcon;
                }
            }
            else if (weather.PreferredColorIndex is { } idx
                     && idx >= 0
                     && idx < AmbientSpacePalette.Colors.Length)
            {
                color = AmbientSpacePalette.Colors[idx];
                highlight = Color.InterpolateBetween(color, Color.White, 0.35f);
            }
        }

        var blip = EnsureComp<RadarBlipComponent>(uid);
        blip.RadarColor = color;
        blip.HighlightedRadarColor = highlight;
        blip.Scale = 1f;
        blip.Shape = RadarBlipShape.Circle;
        blip.VisibleFromOtherGrids = true;
        blip.RequireNoGrid = true;
        blip.MaxDistance = 1812f;
        blip.Enabled = true;
        Dirty(uid, blip);

        var icon = EnsureComp<RadarBlipIconComponent>(uid);
        icon.Icon = iconPath;
        icon.SecondaryIcon = secondaryPath;
        icon.Scale = 1.7f;
        icon.MaxDistance = 1812f;
        icon.AllowWhenHidden = true;
        Dirty(uid, icon);
    }

    private static bool OverlapsPlaced(
        List<(Vector2 Pos, float Radius)> placed,
        Vector2 pos,
        float radius,
        float spacingFactor,
        float minCenterSeparation)
    {
        var factor = Math.Clamp(spacingFactor, 0.05f, 2f);
        var floorSep = MathF.Max(0f, minCenterSeparation);

        foreach (var (otherPos, otherRadius) in placed)
        {
            var minDist = MathF.Max((radius + otherRadius) * factor, floorSep);
            if ((pos - otherPos).LengthSquared() < minDist * minDist)
                return true;
        }

        return false;
    }

    private bool OverlapsStation(MapId mapId, Vector2 pos, float clearance)
    {
        var stationQuery = EntityQueryEnumerator<StationDataComponent>();
        while (stationQuery.MoveNext(out _, out var stationData))
        {
            foreach (var gridUid in stationData.Grids)
            {
                if (TerminatingOrDeleted(gridUid))
                    continue;

                if (!TryComp(gridUid, out TransformComponent? xform) || xform.MapID != mapId)
                    continue;

                if (!TryComp(gridUid, out MapGridComponent? grid))
                    continue;

                var gridPos = _transform.GetWorldPosition(xform);
                var extent = MathF.Max(grid.LocalAABB.Width, grid.LocalAABB.Height) * 0.75f;
                var minDist = clearance + extent;
                if ((pos - gridPos).LengthSquared() < minDist * minDist)
                    return true;
            }
        }

        return false;
    }

    private void ApplyDeterministicVisuals(AmbientSpaceFieldComponent field, int seed, float radius)
    {
        field.Seed = seed;
        field.Density = AmbientSpacePalette.DensityFromSeed(seed);
        field.Radius = radius;
        field.Color = AmbientSpacePalette.ColorFromSeed(seed);
        field.Weather = RollWeather(seed, radius);
        field.Weathers.Clear();
        if (field.Weather is not { } primaryWeather)
            return;

        field.Weathers.Add(primaryWeather);
        var colorIdx = AmbientSpacePalette.ColorIndexFromSeed(seed);
        var colorWeather = WeatherByColorIndex[Math.Clamp(colorIdx, 0, WeatherByColorIndex.Length - 1)];
        if (colorWeather != primaryWeather && CanAddSecondaryWeather(colorWeather, seed, radius))
            field.Weathers.Add(colorWeather);
    }

    private bool CanAddSecondaryWeather(ProtoId<NebulaWeatherPrototype> weatherId, int seed, float radius)
    {
        if (!_prototypes.HasIndex(weatherId))
            return false;

        if (weatherId != RadiationFogWeather)
            return true;

        var radiationWeight = Math.Clamp(
            (radius - RadiationFogMinRadius) / (RadiationFogFullWeightRadius - RadiationFogMinRadius),
            0f,
            1f);
        var radiationRoll = (unchecked((uint) seed) >> 24 & 0xFFu) / 255f;
        return radiationWeight > 0f && radiationRoll < radiationWeight;
    }

    private ProtoId<NebulaWeatherPrototype>? RollWeather(int seed, float radius)
    {
        var u = unchecked((uint) seed);
        var weatherRoll = (u >> 12 & 0xFFFu) / 4095f;
        if (weatherRoll >= WeatherSpawnChance)
            return null;

        var radiationWeight = Math.Clamp((radius - RadiationFogMinRadius) / (RadiationFogFullWeightRadius - RadiationFogMinRadius), 0f, 1f);
        var radiationRoll = (u >> 24 & 0xFFu) / 255f;
        if (radiationWeight > 0f && radiationRoll < radiationWeight * 0.35f && _prototypes.HasIndex(RadiationFogWeather))
            return RadiationFogWeather;

        var colorIdx = AmbientSpacePalette.ColorIndexFromSeed(seed);
        var preferred = WeatherByColorIndex[Math.Clamp(colorIdx, 0, WeatherByColorIndex.Length - 1)];
        if (preferred == RadiationFogWeather && radiationRoll >= radiationWeight)
            preferred = WeatherByColorIndex[Math.Abs((colorIdx + 1) % WeatherByColorIndex.Length)];

        var offPalette = ((u >> 4) & 0xFFu) / 255f < WeatherOffPaletteChance;
        if (!offPalette)
            return _prototypes.HasIndex(preferred) ? preferred : (ProtoId<NebulaWeatherPrototype>?) null;

        var pickIdx = (int) ((u >> 8) % (uint) WeatherByColorIndex.Length);
        var alt = WeatherByColorIndex[pickIdx];
        if (alt == RadiationFogWeather && radiationRoll >= radiationWeight)
            alt = preferred;

        if (_prototypes.HasIndex(alt))
            return alt;

        if (_prototypes.HasIndex(preferred))
            return preferred;

        return null;
    }
}
