// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server._Lua.Stargate.Components;
using Content.Server._Lua.Stargate.Events;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Parallax;
using Content.Server.Procedural;
using Content.Shared.Construction.EntitySystems;
using Content.Shared._Lua.Stargate;
using Content.Shared._Lua.Stargate.Components;
using Content.Shared.Atmos;
using Content.Shared.EntityTable;
using Content.Shared.Physics;
using Content.Shared.Maps;
using Content.Shared._Lua.Stargate.PlanetQuest;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Parallax.Biomes.Markers;
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
    [Dependency] private readonly PlanetQuest.PlanetQuestSystem _planetQuest = default!;

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
        ent.Comp.ProgressiveLoadingActive = true;

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
        try
        {
            var dungeons = await GenerateDungeonsAsync(mapUid, grid, preset, origin, seed, random);

            if (!TryComp<MapGridComponent>(mapUid, out var gridAfter))
                return;
            var dungeonFaction = SpawnBudgetMobs(mapUid, gridAfter, preset, dungeons, origin, random);

            if (!TryComp<BiomeComponent>(mapUid, out var biomeComp))
                return;
            AddLootLayers(mapUid, biomeComp, preset, random);
            AddMobLayers(mapUid, biomeComp, preset, random, dungeonFaction);
            SpawnQuestTargets(mapUid, gridAfter, preset, origin, random);
        }
        finally
        {
            if (TryComp<StargateDestinationComponent>(mapUid, out var destination))
                destination.ProgressiveLoadingActive = false;
        }
    }

    private static readonly int[] DungeonCountWeights = { 9, 8, 7, 6, 5, 4, 3, 2, 1 };
    private static readonly ProtoId<DungeonConfigPrototype>[] DefaultDungeonConfigPool =
    {
        "GateMineshaft", "GateTinyOutpost", "GateSmallOutpost", "GateMediumOutpost",
        "GateTinyLab", "GateSmallLab", "GateQuadBunker", "GateCrossBunker",
        "GateCompactCache", "GateWideShelter", "GateLineOutpost", "GateScatteredCaches",
        "GateHauntedOutpost", "GateLabRuins", "GateLavaOutpost", "GateCaveFactory", "GateMixed"
    };

    private const int DungeonOverlapPadding = 8;
    private const int DungeonPlacementRetries = 5;

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

        var placedBounds = new List<(int MinX, int MinY, int MaxX, int MaxY)>();

        for (var d = 0; d < dungeonCount; d++)
        {
            var configId = configPool[d % configPool.Count];

            if (!_protoManager.TryIndex<DungeonConfigPrototype>(configId, out var dungeonConfig))
                continue;

            Vector2i dungeonPosition = default;
            var placed = false;

            for (var attempt = 0; attempt < DungeonPlacementRetries; attempt++)
            {
                var distance = random.Next(preset.DungeonDistanceMin, preset.DungeonDistanceMax + 1);
                var angle = baseAngle + d * angleStep + (random.NextDouble() - 0.5) * 0.5;
                if (attempt > 0)
                {
                    angle += (random.NextDouble() - 0.5) * 1.2;
                    distance += random.Next(10, 30);
                }

                var offset = new Vector2i(
                    (int)(Math.Cos(angle) * distance),
                    (int)(Math.Sin(angle) * distance));
                dungeonPosition = origin + offset;

                if (!OverlapsExisting(dungeonPosition, placedBounds))
                {
                    placed = true;
                    break;
                }
            }

            if (!placed)
                continue;

            var dungeons = await _dungeon.GenerateDungeonAsync(dungeonConfig, dungeonConfig.ID, gridUid, grid, dungeonPosition, seed + d + 1);

            foreach (var dun in dungeons)
            {
                if (dun.AllTiles.Count == 0)
                    continue;
                var minX = int.MaxValue;
                var minY = int.MaxValue;
                var maxX = int.MinValue;
                var maxY = int.MinValue;
                foreach (var tile in dun.AllTiles)
                {
                    if (tile.X < minX) minX = tile.X;
                    if (tile.Y < minY) minY = tile.Y;
                    if (tile.X > maxX) maxX = tile.X;
                    if (tile.Y > maxY) maxY = tile.Y;
                }
                placedBounds.Add((minX - DungeonOverlapPadding, minY - DungeonOverlapPadding,
                    maxX + DungeonOverlapPadding, maxY + DungeonOverlapPadding));
            }

            result.AddRange(dungeons);
        }

        return result;
    }

    private static bool OverlapsExisting(Vector2i position, List<(int MinX, int MinY, int MaxX, int MaxY)> bounds)
    {
        foreach (var (minX, minY, maxX, maxY) in bounds)
        {
            if (position.X >= minX && position.X <= maxX && position.Y >= minY && position.Y <= maxY)
                return true;
        }
        return false;
    }

    private const int StargateSafeRadiusTiles = 18;

    private string? SpawnBudgetMobs(
        EntityUid gridUid,
        MapGridComponent grid,
        StargatePlanetPresetPrototype preset,
        List<Dungeon> dungeons,
        Vector2i gateOrigin,
        Random random)
    {
        if (preset.DungeonMobCap <= 0 || preset.DungeonMobDensity <= 0 || dungeons.Count == 0)
            return null;

        if (!_protoManager.TryIndex(preset.DungeonMobTable, out var mobTable))
            return null;

        var factionEntities = _entTable.GetSpawns(mobTable, random).ToList();
        if (factionEntities.Count == 0)
            return null;
        var factionProto = factionEntities[0];

        var capLeft = preset.DungeonMobCap;
        var safeRadiusSq = StargateSafeRadiusTiles * StargateSafeRadiusTiles;

        foreach (var dungeon in dungeons)
        {
            foreach (var room in dungeon.Rooms)
            {
                if (capLeft <= 0)
                    return factionProto;

                var tiles = room.Tiles.ToList();
                if (tiles.Count == 0)
                    continue;

                var desiredCount = Math.Clamp(
                    tiles.Count / preset.DungeonMobDensity,
                    preset.DungeonMobsPerRoomMin,
                    preset.DungeonMobsPerRoomMax);

                for (var m = 0; m < desiredCount && capLeft > 0; m++)
                {
                    Vector2i? tile = null;
                    for (var attempt = 0; attempt < Math.Min(tiles.Count, 20) && tile == null; attempt++)
                    {
                        var t = tiles[random.Next(tiles.Count)];
                        var dt = t - gateOrigin;
                        if (dt.X * dt.X + dt.Y * dt.Y <= safeRadiusSq)
                            continue;
                        if (_anchorable.TileFree((gridUid, grid), t, (int)CollisionGroup.MachineLayer,
                                (int)CollisionGroup.MachineLayer))
                            tile = t;
                    }

                    if (tile == null)
                        continue;

                    SpawnAtPosition(factionProto, _maps.GridTileToLocal(gridUid, grid, tile.Value));
                    capLeft--;
                }
            }
        }

        return factionProto;
    }

    private void SpawnQuestTargets(
        EntityUid mapUid,
        MapGridComponent grid,
        StargatePlanetPresetPrototype preset,
        Vector2i origin,
        Random random)
    {
        var questPool = preset.QuestPrototypes.Count > 0
            ? preset.QuestPrototypes.Select(id => _protoManager.Index<PlanetQuestPrototype>(id)).ToList()
            : _protoManager.EnumeratePrototypes<PlanetQuestPrototype>().ToList();

        if (questPool.Count == 0)
            return;

        var questProto = questPool[random.Next(questPool.Count)];

        var structureCount = 0;
        if (questProto.StructureCountMax > 0)
        {
            var min = Math.Max(0, questProto.StructureCountMin);
            var max = Math.Max(min, questProto.StructureCountMax);
            structureCount = random.Next(min, max + 1);
        }

        var bossCount = Math.Max(0, questProto.BossCount);
        var safeRadiusSq = StargateSafeRadiusTiles * StargateSafeRadiusTiles;

        _planetQuest.SetupQuest(
            mapUid,
            structureCount,
            bossCount,
            questProto.RewardMin,
            questProto.RewardMax,
            questProto.RewardMultiplier,
            questProto.Name,
            questProto.Description,
            random);

        if (structureCount > 0 && questProto.StructurePrototypes.Count > 0)
        {
            for (var i = 0; i < structureCount; i++)
            {
                var protoId = questProto.StructurePrototypes[random.Next(questProto.StructurePrototypes.Count)];
                var tile = FindQuestSpawnTile(grid, mapUid, origin, safeRadiusSq, 40, 120, random);
                EnsureQuestSpawnPlatform(mapUid, grid, tile, 1, random);
                ClearQuestSpawnArea(mapUid, grid, tile, 1);
                var uid = SpawnAtPosition(protoId, _maps.GridTileToLocal(mapUid, grid, tile));
                _planetQuest.RegisterTarget(uid, mapUid, PlanetObjectiveType.DestroyStructures);
            }
        }

        if (bossCount > 0 && questProto.BossPrototypes.Count > 0)
        {
            for (var i = 0; i < bossCount; i++)
            {
                var bossProtoId = questProto.BossPrototypes[random.Next(questProto.BossPrototypes.Count)];
                var tile = FindQuestSpawnTile(grid, mapUid, origin, safeRadiusSq, 60, 150, random);
                EnsureQuestSpawnPlatform(mapUid, grid, tile, 2, random);
                ClearQuestSpawnArea(mapUid, grid, tile, 2);
                var uid = SpawnAtPosition(bossProtoId, _maps.GridTileToLocal(mapUid, grid, tile));
                _planetQuest.RegisterTarget(uid, mapUid, PlanetObjectiveType.KillBoss);
            }
        }
    }

    private void EnsureQuestSpawnPlatform(
        EntityUid mapUid,
        MapGridComponent grid,
        Vector2i centerTile,
        int radius,
        Random random)
    {
        var tileDef = _tileDefManager["FloorSteel"];
        var tiles = new List<(Vector2i Index, Tile Tile)>();

        for (var dx = -radius; dx <= radius; dx++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                if (dx * dx + dy * dy > radius * radius)
                    continue;

                var tile = centerTile + new Vector2i(dx, dy);
                tiles.Add((tile, new Tile(tileDef.TileId,
                    variant: _tile.PickVariant((ContentTileDefinition)tileDef, random))));
            }
        }

        _maps.SetTiles(mapUid, grid, tiles);
    }

    private void ClearQuestSpawnArea(EntityUid mapUid, MapGridComponent grid, Vector2i centerTile, int radius)
    {
        for (var dx = -radius; dx <= radius; dx++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                var tile = centerTile + new Vector2i(dx, dy);
                var anchored = _maps.GetAnchoredEntitiesEnumerator(mapUid, grid, tile);
                while (anchored.MoveNext(out var ent))
                {
                    QueueDel(ent.Value);
                }
            }
        }
    }

    private Vector2i FindQuestSpawnTile(
        MapGridComponent grid,
        EntityUid gridUid,
        Vector2i origin,
        int safeRadiusSq,
        int minDist,
        int maxDist,
        Random random)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            var angle = random.NextDouble() * Math.PI * 2;
            var dist = random.Next(minDist, maxDist + 1);
            var tile = origin + new Vector2i((int)(Math.Cos(angle) * dist), (int)(Math.Sin(angle) * dist));

            var dt = tile - origin;
            if (dt.X * dt.X + dt.Y * dt.Y <= safeRadiusSq)
                continue;

            if (_anchorable.TileFree((gridUid, grid), tile, (int)CollisionGroup.MachineLayer, (int)CollisionGroup.MachineLayer))
                return tile;
        }

        for (var attempt = 0; attempt < 30; attempt++)
        {
            var angle = random.NextDouble() * Math.PI * 2;
            var dist = random.Next(minDist, maxDist + 1);
            var tile = origin + new Vector2i((int)(Math.Cos(angle) * dist), (int)(Math.Sin(angle) * dist));

            var dt = tile - origin;
            if (dt.X * dt.X + dt.Y * dt.Y <= safeRadiusSq)
                continue;

            if (_maps.TryGetTileRef(gridUid, grid, tile, out var tileRef) && !tileRef.Tile.IsEmpty)
                return tile;
        }

        var allTiles = _maps.GetAllTilesEnumerator(gridUid, grid);
        while (allTiles.MoveNext(out var tileRef))
        {
            var tile = tileRef.Value.GridIndices;
            if (tileRef.Value.Tile.IsEmpty)
                continue;

            var dt = tile - origin;
            if (dt.X * dt.X + dt.Y * dt.Y <= safeRadiusSq)
                continue;

            return tile;
        }

        return origin + new Vector2i(Math.Max(minDist, StargateSafeRadiusTiles + 2), 0);
    }

    private static int PickWeightedDungeonCount(Random random, StargatePlanetPresetPrototype preset)
    {
        var min = Math.Clamp(preset.DungeonCountMin, 0, 8);
        var max = Math.Clamp(preset.DungeonCountMax, 0, 8);
        var totalWeight = 0;
        for (var i = min; i <= max; i++) totalWeight += DungeonCountWeights[i];
        if (totalWeight <= 0) return 0;
        var roll = random.Next(totalWeight);
        var acc = 0;
        for (var i = min; i <= max; i++)
        { acc += DungeonCountWeights[i]; if (roll < acc) return i; }
        return max;
    }

    private List<ProtoId<DungeonConfigPrototype>> BuildConfigPool(
        StargatePlanetPresetPrototype preset, Random random)
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
        Random random,
        string? dungeonFaction)
    {
        switch (preset.MobSpawnMode)
        {
            case MobSpawnMode.Surface:
            case MobSpawnMode.Both:
                AddSurfaceMobs(uid, biome, preset, random, dungeonFaction);
                break;

            case MobSpawnMode.DungeonOnly:
                if (preset.RareSurfaceMobChance > 0 && random.NextDouble() < preset.RareSurfaceMobChance)
                    AddSurfaceMobs(uid, biome, preset, random, dungeonFaction, preset.RareSurfaceMobLayers, preset.RareSurfaceMobLayerCount);
                break;

            case MobSpawnMode.None:
                break;
        }
    }

    private void AddSurfaceMobs(
        EntityUid uid,
        BiomeComponent biome,
        StargatePlanetPresetPrototype preset,
        Random random,
        string? dungeonFaction,
        List<ProtoId<BiomeMarkerLayerPrototype>>? overrideLayers = null,
        int? overrideCount = null)
    {
        var sourceLayers = overrideLayers ?? preset.MobLayers;
        var count = overrideCount ?? preset.MobLayerCount;
        if (sourceLayers.Count == 0 || count <= 0)
            return;
        var candidates = sourceLayers.ToList();

        if (dungeonFaction != null && candidates.Count > 1)
        {
            var nonDungeon = candidates
                .Where(id =>
                {
                    var proto = _protoManager.Index<BiomeMarkerLayerPrototype>(id);
                    return proto.Prototype != dungeonFaction;
                })
                .ToList();

            if (nonDungeon.Count > 0)
                candidates = nonDungeon;
        }

        for (var i = 0; i < count && candidates.Count > 0; i++)
        {
            var layerIdx = random.Next(candidates.Count);
            var layer = candidates[layerIdx];
            candidates.RemoveAt(layerIdx);
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
