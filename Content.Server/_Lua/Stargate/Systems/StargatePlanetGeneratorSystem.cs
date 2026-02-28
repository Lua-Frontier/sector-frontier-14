// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server._Lua.Stargate.Components;
using Content.Server._Lua.Stargate.Events;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Ghost.Roles.Components;
using Content.Server.NPC.Systems;
using Content.Server.Parallax;
using Content.Server.Procedural;
using Content.Shared.Construction.EntitySystems;
using Content.Shared._Lua.Stargate;
using Content.Shared._Lua.Stargate.Components;
using Content.Shared.Atmos;
using Content.Shared.EntityTable;
using Content.Shared.Physics;
using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Procedural;
using Content.Shared.Salvage;
using Content.Shared.Salvage.Expeditions.Modifiers;
using Content.Shared.Weather;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Content.Server._Lua.Stargate.Systems;

public sealed class StargatePlanetGeneratorSystem : EntitySystem
{
    [Dependency] private readonly AnchorableSystem _anchorable = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly EntityTableSystem _entTable = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefManager = default!;
    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly DungeonSystem _dungeon = default!;
    [Dependency] private readonly MetaDataSystem _metadata = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly SharedSalvageSystem _salvage = default!;
    [Dependency] private readonly SharedWeatherSystem _weather = default!;
    [Dependency] private readonly TileSystem _tile = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StargateDestinationComponent, StargateOpenEvent>(OnStargateOpen);
    }

    private void OnStargateOpen(Entity<StargateDestinationComponent> ent, ref StargateOpenEvent args)
    {
        if (ent.Comp.Loaded)
            return;

        ent.Comp.Loaded = true;

        if (!TryComp<MapGridComponent>(ent.Owner, out var grid))
            return;

        var seed = ent.Comp.Seed;
        var origin = ent.Comp.Origin;
        var random = new Random(seed);

        var presetId = GetPresetForSeed(seed);
        if (presetId == null || !_protoManager.TryIndex<StargatePlanetPresetPrototype>(presetId, out var preset))
            return;

        _ = RunAsyncPlanetGen(ent.Owner, grid, preset, seed, origin, random);
    }

    private async Task RunAsyncPlanetGen(
        EntityUid mapUid,
        MapGridComponent grid,
        StargatePlanetPresetPrototype preset,
        int seed,
        Vector2i origin,
        Random random)
    {
        var dungeons = await GenerateDungeonsAsync(mapUid, grid, preset, origin, seed, random);

        if (!TryComp<MapGridComponent>(mapUid, out var gridAfter))
            return;
        SpawnBudgetMobs(mapUid, gridAfter, preset, dungeons, random);

        if (!TryComp<BiomeComponent>(mapUid, out var biomeComp))
            return;
        AddLootLayers(mapUid, biomeComp, preset, random);
        AddMobLayers(mapUid, biomeComp, preset, random);
    }

    private static readonly int[] DungeonCountWeights = { 9, 8, 7, 6, 5, 4, 3, 2, 1 };
    private static readonly ProtoId<DungeonConfigPrototype>[] DefaultDungeonConfigPool =
    {
        "GateMineshaft", "GateTinyOutpost", "GateSmallOutpost", "GateMediumOutpost",
        "GateTinyLab", "GateSmallLab", "GateQuadBunker", "GateCrossBunker",
        "GateCompactCache", "GateWideShelter", "GateLineOutpost", "GateScatteredCaches",
        "GateHauntedOutpost", "GateLabRuins", "GateLavaOutpost", "GateCaveFactory", "GateMixed"
    };

    private async Task<List<Dungeon>> GenerateDungeonsAsync(
        EntityUid gridUid,
        MapGridComponent grid,
        StargatePlanetPresetPrototype preset,
        Vector2i origin,
        int seed,
        Random random)
    {
        var result = new List<Dungeon>();
        var dungeonCount = PickWeightedDungeonCount(random, preset);
        if (dungeonCount <= 0)
            return result;

        var configPool = BuildConfigPool(preset, random);
        if (configPool.Count == 0)
            return result;

        var baseAngle = random.NextDouble() * 2 * Math.PI;
        var angleStep = dungeonCount > 1 ? 2 * Math.PI / dungeonCount : 0;

        for (var d = 0; d < dungeonCount; d++)
        {
            var configId = configPool[d % configPool.Count];

            if (!_protoManager.TryIndex<DungeonConfigPrototype>(configId, out var dungeonConfig))
                continue;

            var distance = random.Next(preset.DungeonDistanceMin, preset.DungeonDistanceMax + 1);
            var angle = baseAngle + d * angleStep + (random.NextDouble() - 0.5) * 0.5;
            var offset = new Vector2i(
                (int)(Math.Cos(angle) * distance),
                (int)(Math.Sin(angle) * distance));
            var dungeonPosition = origin + offset;

            var dungeons = await _dungeon.GenerateDungeonAsync(dungeonConfig, dungeonConfig.ID, gridUid, grid, dungeonPosition, seed + d + 1);
            result.AddRange(dungeons);
        }

        return result;
    }

    private void SpawnBudgetMobs(
        EntityUid gridUid,
        MapGridComponent grid,
        StargatePlanetPresetPrototype preset,
        List<Dungeon> dungeons,
        Random random)
    {
        if (preset.DungeonMobBudget <= 0 || dungeons.Count == 0)
            return;

        if (!_protoManager.TryIndex(preset.DungeonMobTable, out var mobTable))
            return;

        var npcs = EntityManager.System<NPCSystem>();
        var mobBudget = preset.DungeonMobBudget;
        const float CostPerMob = 1f;

        var allRooms = new List<DungeonRoom>();
        foreach (var dungeon in dungeons)
            allRooms.AddRange(dungeon.Rooms);

        if (allRooms.Count == 0)
            return;

        while (mobBudget >= CostPerMob)
        {
            mobBudget -= CostPerMob;

            var room = allRooms[random.Next(allRooms.Count)];
            var tiles = room.Tiles.ToList();
            if (tiles.Count == 0)
                continue;

            Vector2i? tile = null;
            for (var attempt = 0; attempt < tiles.Count && tile == null; attempt++)
            {
                var t = tiles[random.Next(tiles.Count)];
                if (_anchorable.TileFree((gridUid, grid), t, (int)CollisionGroup.MachineLayer,
                        (int)CollisionGroup.MachineLayer))
                    tile = t;
            }

            if (tile == null)
                continue;

            var entities = _entTable.GetSpawns(mobTable, random).ToList();
            if (entities.Count == 0)
                continue;

            var uid = SpawnAtPosition(entities[0], _maps.GridTileToLocal(gridUid, grid, tile.Value));
            RemComp<GhostRoleComponent>(uid);
            RemComp<GhostTakeoverAvailableComponent>(uid);
            npcs.SleepNPC(uid);
        }
    }

    private static int PickWeightedDungeonCount(Random random, StargatePlanetPresetPrototype preset)
    {
        var min = Math.Clamp(preset.DungeonCountMin, 0, 8);
        var max = Math.Clamp(preset.DungeonCountMax, 0, 8);
        var totalWeight = 0;
        for (var i = min; i <= max; i++)
            totalWeight += DungeonCountWeights[i];
        if (totalWeight <= 0) return 0;
        var roll = random.Next(totalWeight);
        var acc = 0;
        for (var i = min; i <= max; i++)
        {
            acc += DungeonCountWeights[i];
            if (roll < acc) return i;
        }
        return max;
    }

    private List<ProtoId<DungeonConfigPrototype>> BuildConfigPool(
        StargatePlanetPresetPrototype preset,
        Random random)
    {
        var pool = new List<ProtoId<DungeonConfigPrototype>>();

        if (preset.DungeonConfigs is { Count: > 0 })
        {
            foreach (var id in preset.DungeonConfigs)
            {
                if (_protoManager.HasIndex<DungeonConfigPrototype>(id))
                    pool.Add(id);
            }
        }
        else if (preset.DungeonConfig != null && _protoManager.HasIndex<DungeonConfigPrototype>(preset.DungeonConfig.Value))
        {
            pool.Add(preset.DungeonConfig.Value);
        }

        if (pool.Count == 0)
        {
            foreach (var id in DefaultDungeonConfigPool)
            {
                if (_protoManager.HasIndex<DungeonConfigPrototype>(id))
                    pool.Add(id);
            }
        }

        for (var i = pool.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        return pool;
    }

    private void AddLootLayers(
        EntityUid uid,
        BiomeComponent biome,
        StargatePlanetPresetPrototype preset,
        Random random)
    {
        if (preset.LootLayers.Count == 0)
            return;

        if (preset.LootLayerCount <= 0)
        {
            foreach (var layer in preset.LootLayers)
                _biome.AddMarkerLayer(uid, biome, layer.Id);
        }
        else
        {
            var lootLayers = preset.LootLayers.ToList();
            var count = Math.Min(preset.LootLayerCount, lootLayers.Count);
            for (var i = 0; i < count; i++)
            {
                var layerIdx = random.Next(lootLayers.Count);
                var layer = lootLayers[layerIdx];
                lootLayers.RemoveAt(layerIdx);
                _biome.AddMarkerLayer(uid, biome, layer.Id);
            }
        }
    }

    private void AddMobLayers(
        EntityUid uid,
        BiomeComponent biome,
        StargatePlanetPresetPrototype preset,
        Random random)
    {
        switch (preset.MobSpawnMode)
        {
            case MobSpawnMode.Surface:
            case MobSpawnMode.Both:
                AddSurfaceMobs(uid, biome, preset, random);
                break;

            case MobSpawnMode.DungeonOnly:
                if (preset.RareSurfaceMobChance > 0 && random.NextDouble() < preset.RareSurfaceMobChance)
                    AddRareSurfaceMobs(uid, biome, preset, random);
                break;

            case MobSpawnMode.None:
                break;
        }
    }

    private void AddSurfaceMobs(
        EntityUid uid,
        BiomeComponent biome,
        StargatePlanetPresetPrototype preset,
        Random random)
    {
        var mobLayers = preset.MobLayers.ToList();
        for (var i = 0; i < preset.MobLayerCount && mobLayers.Count > 0; i++)
        {
            var layerIdx = random.Next(mobLayers.Count);
            var layer = mobLayers[layerIdx];
            mobLayers.RemoveAt(layerIdx);
            _biome.AddMarkerLayer(uid, biome, layer.Id);
        }
    }

    private void AddRareSurfaceMobs(
        EntityUid uid,
        BiomeComponent biome,
        StargatePlanetPresetPrototype preset,
        Random random)
    {
        var rareLayers = preset.RareSurfaceMobLayers.ToList();
        for (var i = 0; i < preset.RareSurfaceMobLayerCount && rareLayers.Count > 0; i++)
        {
            var layerIdx = random.Next(rareLayers.Count);
            var layer = rareLayers[layerIdx];
            rareLayers.RemoveAt(layerIdx);
            _biome.AddMarkerLayer(uid, biome, layer.Id);
        }
    }

    public (EntityUid MapUid, EntityUid GateUid) CreateDestinationMap(byte[] address, int seed)
    {
        var presetId = GetPresetForSeed(seed);
        StargatePlanetPresetPrototype? preset = null;
        if (presetId != null)
            _protoManager.TryIndex(presetId, out preset);

        preset ??= GetDefaultPreset();

        var random = new Random(seed);
        var mapUid = _maps.CreateMap();

        var planetName = _salvage.GetFTLName(_protoManager.Index(preset.NameDataset), seed);
        _metadata.SetEntityName(mapUid, planetName);

        const int MaxOffset = 256;
        var origin = new Vector2i(random.Next(-MaxOffset, MaxOffset), random.Next(-MaxOffset, MaxOffset));

        var worldRadius = preset.WorldRadiusMin
            + (float)(random.NextDouble() * (preset.WorldRadiusMax - preset.WorldRadiusMin));

        var restricted = new RestrictedRangeComponent
        {
            Range = worldRadius,
            Origin = origin
        };
        AddComp(mapUid, restricted);

        var biomeId = preset.Biome[random.Next(preset.Biome.Count)];
        _biome.EnsurePlanet(mapUid, _protoManager.Index(biomeId), seed);

        ApplyEnvironmentMods(mapUid, preset, random);

        var grid = Comp<MapGridComponent>(mapUid);

        BuildGatePlatform(mapUid, grid, origin, preset.GateSafeRadius, random);

        var originCoords = new EntityCoordinates(mapUid, origin);

        var dest = EnsureComp<StargateDestinationComponent>(mapUid);
        dest.Address = address;
        dest.Seed = seed;
        dest.Origin = origin;

        var gateUid = SpawnAtPosition("Stargate", originCoords);
        dest.GateUid = gateUid;

        if (TryComp<StargateComponent>(gateUid, out var gateComp))
            gateComp.Address = address;

        _appearance.SetData(gateUid, StargateVisuals.State, StargateVisualState.Off);

        var consoleUid = SpawnAtPosition("StargateConsole",
            new EntityCoordinates(mapUid, origin + new Vector2i(4, 0)));

        if (TryComp<StargateConsoleComponent>(consoleUid, out var consoleComp))
        {
            consoleComp.LinkedStargate = gateUid;
        }

        return (mapUid, gateUid);
    }

    private void BuildGatePlatform(
        EntityUid mapUid,
        MapGridComponent grid,
        Vector2i origin,
        float safeRadius,
        Random random)
    {
        var tileDef = _tileDefManager["FloorSteel"];
        var tiles = new List<(Vector2i Index, Tile Tile)>();
        var r = (int)Math.Ceiling(safeRadius);

        for (var x = -r; x <= r; x++)
        {
            for (var y = -r; y <= r; y++)
            {
                if (x * x + y * y > r * r)
                    continue;

                tiles.Add((new Vector2i(x, y) + origin, new Tile(tileDef.TileId,
                    variant: _tile.PickVariant((ContentTileDefinition) tileDef, random))));
            }
        }

        _maps.SetTiles(mapUid, grid, tiles);
    }

    private void ApplyEnvironmentMods(EntityUid mapUid, StargatePlanetPresetPrototype preset, Random random)
    {
        ApplyAtmosphereMods(mapUid, preset, random);
        ApplyLightMod(mapUid, preset, random);
        ApplyWeatherMod(mapUid, preset, random);
    }

    private void ApplyAtmosphereMods(EntityUid mapUid, StargatePlanetPresetPrototype preset, Random random)
    {
        float? temperature = null;

        if (preset.TemperatureMods is { Count: > 0 })
        {
            var tempModId = preset.TemperatureMods[random.Next(preset.TemperatureMods.Count)];
            if (_protoManager.TryIndex(tempModId, out var tempMod))
                temperature = tempMod.Temperature;
        }

        if (preset.AirMods is { Count: > 0 })
        {
            var airModId = preset.AirMods[random.Next(preset.AirMods.Count)];
            if (_protoManager.TryIndex(airModId, out var airMod))
            {
                if (airMod.Space)
                {
                    var emptyMix = new GasMixture(new float[Atmospherics.AdjustedNumberOfGases],
                        temperature ?? Atmospherics.T20C);
                    _atmosphere.SetMapAtmosphere(mapUid, true, emptyMix);
                }
                else
                {
                    var gasMoles = new float[Atmospherics.AdjustedNumberOfGases];
                    Array.Copy(airMod.Gases, gasMoles, Math.Min(airMod.Gases.Length, gasMoles.Length));
                    var mix = new GasMixture(gasMoles, temperature ?? Atmospherics.T20C);
                    _atmosphere.SetMapAtmosphere(mapUid, false, mix);
                }
                return;
            }
        }

        if (temperature != null)
        {
            var moles = new float[Atmospherics.AdjustedNumberOfGases];
            moles[(int)Gas.Oxygen] = 21.824779f;
            moles[(int)Gas.Nitrogen] = 82.10312f;
            _atmosphere.SetMapAtmosphere(mapUid, false, new GasMixture(moles, temperature.Value));
        }
    }

    private void ApplyLightMod(EntityUid mapUid, StargatePlanetPresetPrototype preset, Random random)
    {
        if (preset.LightMods is not { Count: > 0 })
            return;

        var lightModId = preset.LightMods[random.Next(preset.LightMods.Count)];
        if (!_protoManager.TryIndex(lightModId, out var lightMod) || lightMod.Color == null)
            return;

        var lighting = EnsureComp<MapLightComponent>(mapUid);
        lighting.AmbientLightColor = lightMod.Color.Value;
        Dirty(mapUid, lighting);
    }

    private void ApplyWeatherMod(EntityUid mapUid, StargatePlanetPresetPrototype preset, Random random)
    {
        if (preset.WeatherMods is not { Count: > 0 })
            return;

        var weatherModId = preset.WeatherMods[random.Next(preset.WeatherMods.Count)];
        if (!_protoManager.TryIndex(weatherModId, out var weatherMod))
            return;

        if (!_protoManager.TryIndex<WeatherPrototype>(weatherMod.WeatherPrototype, out var weatherProto))
            return;

        var mapId = Transform(mapUid).MapID;
        _weather.SetWeather(mapId, weatherProto, null);
    }

    private string? GetPresetForSeed(int seed)
    {
        var presets = new List<(string Id, float Weight)>();
        foreach (var proto in _protoManager.EnumeratePrototypes<StargatePlanetPresetPrototype>())
        {
            presets.Add((proto.ID, proto.Weight));
        }

        if (presets.Count == 0)
            return null;

        var random = new Random(seed);
        var totalWeight = 0f;
        foreach (var (_, w) in presets)
            totalWeight += w;

        var roll = (float)(random.NextDouble() * totalWeight);
        var accumulated = 0f;

        foreach (var (id, w) in presets)
        {
            accumulated += w;
            if (roll < accumulated)
                return id;
        }

        return presets[^1].Id;
    }

    private StargatePlanetPresetPrototype GetDefaultPreset()
    {
        return new StargatePlanetPresetPrototype();
    }
}
