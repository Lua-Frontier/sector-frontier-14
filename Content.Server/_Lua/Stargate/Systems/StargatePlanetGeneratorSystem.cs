// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server._Lua.Stargate.Components;
using Content.Server._Lua.Stargate.Events;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Parallax;
using Content.Server.Procedural;
using Content.Shared.Construction.EntitySystems;
using Content.Shared._Lua.Expedition;
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
using Content.Shared.Procedural.DungeonGenerators;
using Content.Shared.Salvage;
using Content.Shared.Salvage.Expeditions.Modifiers;
using Content.Shared.Weather;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
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
            var dungeons = await GenerateDungeonsAsync(mapUid, grid, preset, preset.Biome[0].Id, origin, seed, random, useExpeditionPool: false);

            if (!TryComp<MapGridComponent>(mapUid, out var gridAfter))
                return;
            var dungeonFaction = SpawnBudgetMobs(mapUid, gridAfter, preset, dungeons, origin, random);

            if (!TryComp<BiomeComponent>(mapUid, out var biomeComp))
                return;
            AddLootLayers(mapUid, biomeComp, preset, random);
            AddMobLayers(mapUid, biomeComp, preset, random, dungeonFaction);
            SpawnQuestTargets(mapUid, gridAfter, preset, origin, random, dungeons);
        }
        finally
        {
            if (TryComp<StargateDestinationComponent>(mapUid, out var destination))
                destination.ProgressiveLoadingActive = false;
        }
    }

    private static readonly int[] DungeonCountWeights = { 9, 8, 7, 6, 5, 4, 3, 2, 1 };

    private const int DungeonOverlapPadding = 8;
    private const int DungeonPlacementRetries = 12;

    private async Task<List<Dungeon>> GenerateDungeonsAsync(
        EntityUid gridUid,
        MapGridComponent grid,
        StargatePlanetPresetPrototype preset,
        string biomeId,
        Vector2i origin,
        int seed,
        Random random,
        bool useExpeditionPool = false,
        Action<float>? progress = null)
    {
        var result = new List<Dungeon>();
        if (!preset.DungeonPool)
            return result;

        var dungeonCount = PickWeightedDungeonCount(random, preset);
        if (useExpeditionPool) dungeonCount = 1;
        else if (dungeonCount <= 0)
            return result;

        var configPool = BuildConfigPool(random, preset, biomeId, useExpeditionPool);
        if (configPool.Count == 0)
            return result;

        var configId = configPool[random.Next(configPool.Count)];
        if (!_protoManager.TryIndex<DungeonConfigPrototype>(configId, out var dungeonConfig))
            return result;

        var rangeLimit = GetRestrictedRangeLimit(gridUid);
        var dungeonRadius = MathF.Min(EstimateDungeonRadius(dungeonConfig), rangeLimit * 0.4f);
        var maxCenterDist = MathF.Max(48f, rangeLimit - dungeonRadius);
        var distMin = preset.DungeonDistanceMin;
        var distMax = preset.DungeonDistanceMax;
        if (distMax > (int) maxCenterDist) distMax = Math.Max(48, (int) maxCenterDist);
        if (distMin > distMax) distMin = Math.Max(48, distMax / 2);
        var baseAngle = random.NextDouble() * 2 * Math.PI;
        var angleStep = dungeonCount > 1 ? 2 * Math.PI / dungeonCount : 0;

        var placedBounds = new List<(int MinX, int MinY, int MaxX, int MaxY)>();

        for (var d = 0; d < dungeonCount; d++)
        {
            Vector2i dungeonPosition = default;
            var placed = false;

            for (var attempt = 0; attempt < DungeonPlacementRetries; attempt++)
            {
                var distance = random.Next(distMin, distMax + 1);
                var angle = baseAngle + d * angleStep + (random.NextDouble() - 0.5) * 0.5;
                if (attempt > 0)
                {
                    angle += (random.NextDouble() - 0.5) * 1.2;
                    distance = Math.Clamp(distance + random.Next(-20, 31), distMin, distMax);
                }

                var offset = new Vector2i((int) (Math.Cos(angle) * distance), (int)(Math.Sin(angle) * distance));
                dungeonPosition = origin + offset;

                if (OverlapsExisting(dungeonPosition, placedBounds)) continue;
                placed = true;
                break;
            }
            if (!placed)
            {
                var fallbackDist = Math.Clamp((distMin + distMax) / 2, 48, (int) maxCenterDist);
                var angle = baseAngle + d * angleStep;
                dungeonPosition = origin + new Vector2i(
                    (int) (Math.Cos(angle) * fallbackDist),
                    (int) (Math.Sin(angle) * fallbackDist));
                placed = true;
                Log.Warning($"Expedition dungeon placement exhausted retries; using fallback at {dungeonPosition}");
            }

            var startP = dungeonCount <= 1 ? 0.15f : (float) d / dungeonCount;
            var endP = dungeonCount <= 1 ? 0.95f : (float) (d + 1) / dungeonCount;
            progress?.Invoke(startP);
            var dungeons = await _dungeon.GenerateDungeonAsync(dungeonConfig, dungeonConfig.ID, gridUid, grid, dungeonPosition, seed + d + 1);

            progress?.Invoke(endP);
            await YieldPlanetGenTick();
            foreach (var dun in dungeons)
            {
                if (dun.AllTiles.Count == 0)
                    continue;
                var minX = int.MaxValue;
                var minY = int.MaxValue;
                var maxX = int.MinValue;
                var maxY = int.MinValue;
                var tileCount = 0;
                foreach (var tile in dun.AllTiles)
                {
                    if (tile.X < minX) minX = tile.X;
                    if (tile.Y < minY) minY = tile.Y;
                    if (tile.X > maxX) maxX = tile.X;
                    if (tile.Y > maxY) maxY = tile.Y;
                    tileCount++;
                    if (tileCount % 4096 == 0) await Task.Yield();
                }
                if (!BoundsFitInRestrictedRange(minX, minY, maxX, maxY, origin, rangeLimit)) Log.Warning($"Expedition dungeon at {dungeonPosition} grazes RestrictedRange edge.");
                placedBounds.Add((minX - DungeonOverlapPadding, minY - DungeonOverlapPadding,
                    maxX + DungeonOverlapPadding, maxY + DungeonOverlapPadding));
                result.Add(dun);
            }
            await Task.Yield();
        }
        return result;
    }

    private float GetRestrictedRangeLimit(EntityUid mapUid)
    {
        if (TryComp<RestrictedRangeComponent>(mapUid, out var restricted)) return MathF.Max(96f, restricted.Range * 0.92f);
        return float.MaxValue;
    }

    private float EstimateDungeonRadius(DungeonConfig dungeonConfig)
    {
        var best = 96f;
        foreach (var layer in dungeonConfig.Layers)
        {
            if (layer is not PrefabDunGen prefab) continue;
            foreach (var presetId in prefab.Presets)
            {
                if (!_protoManager.TryIndex(presetId, out DungeonPresetPrototype? preset)) continue;
                var maxSq = 0f;
                foreach (var pack in preset.RoomPacks)
                {
                    Expand(ref maxSq, pack.Left, pack.Bottom);
                    Expand(ref maxSq, pack.Right, pack.Bottom);
                    Expand(ref maxSq, pack.Left, pack.Top);
                    Expand(ref maxSq, pack.Right, pack.Top);
                }
                best = MathF.Max(best, MathF.Sqrt(maxSq));
            }
        }
        return best + 32f;
        static void Expand(ref float maxSq, int x, int y)
        {
            var sq = (float) x * x + (float) y * y;
            if (sq > maxSq) maxSq = sq;
        }
    }

    private static bool BoundsFitInRestrictedRange(int minX, int minY, int maxX, int maxY, Vector2i origin, float rangeLimit)
    {
        return CornerOk(minX, minY) && CornerOk(maxX, minY) && CornerOk(minX, maxY) && CornerOk(maxX, maxY);
        bool CornerOk(int x, int y)
        {
            var dx = x - origin.X;
            var dy = y - origin.Y;
            return dx * dx + dy * dy <= rangeLimit * rangeLimit;
        }
    }

    private static async Task YieldPlanetGenTick()
    { await Task.Yield(); }
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
                if (capLeft <= 0) return factionProto;
                var tiles = room.Tiles.ToList();
                if (tiles.Count == 0) continue;
                var desiredCount = Math.Clamp(tiles.Count / preset.DungeonMobDensity, preset.DungeonMobsPerRoomMin, preset.DungeonMobsPerRoomMax);
                for (var m = 0; m < desiredCount && capLeft > 0; m++)
                {
                    Vector2i? tile = null;
                    for (var attempt = 0; attempt < Math.Min(tiles.Count, 20) && tile == null; attempt++)
                    {
                        var t = tiles[random.Next(tiles.Count)];
                        var dt = t - gateOrigin;
                        if (dt.X * dt.X + dt.Y * dt.Y <= safeRadiusSq) continue;
                        if (_anchorable.TileFree((gridUid, grid), t, (int)CollisionGroup.MachineLayer, (int)CollisionGroup.MachineLayer)) tile = t;
                    }
                    if (tile == null) continue;
                    SpawnAtPosition(factionProto, _maps.GridTileToLocal(gridUid, grid, tile.Value));
                    capLeft--;
                }
            }
        }
        return factionProto;
    }

    private async Task<string?> SpawnBudgetMobsAsync(EntityUid gridUid, MapGridComponent grid, StargatePlanetPresetPrototype preset, List<Dungeon> dungeons, Vector2i gateOrigin, Random random, Action<float>? progress = null)
    {
        if (preset.DungeonMobCap <= 0 || preset.DungeonMobDensity <= 0 || dungeons.Count == 0) return null;
        if (!_protoManager.TryIndex(preset.DungeonMobTable, out var mobTable)) return null;
        var factionEntities = _entTable.GetSpawns(mobTable, random).ToList();
        if (factionEntities.Count == 0) return null;
        var factionProto = factionEntities[0];
        var capLeft = preset.DungeonMobCap;
        var safeRadiusSq = StargateSafeRadiusTiles * StargateSafeRadiusTiles;
        var roomTotal = 0;
        foreach (var dungeon in dungeons) roomTotal += dungeon.Rooms.Count;
        roomTotal = Math.Max(roomTotal, 1);
        var roomsDone = 0;
        var spawnedSinceYield = 0;
        foreach (var dungeon in dungeons)
        {
            foreach (var room in dungeon.Rooms)
            {
                if (capLeft <= 0)
                {
                    progress?.Invoke(1f);
                    return factionProto;
                }

                var tiles = room.Tiles.ToList();
                if (tiles.Count == 0)
                {
                    roomsDone++;
                    continue;
                }

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
                    spawnedSinceYield++;
                    if (spawnedSinceYield >= 8)
                    {
                        spawnedSinceYield = 0;
                        await Task.Yield();
                    }
                }
                roomsDone++;
                progress?.Invoke((float) roomsDone / roomTotal);
            }
        }

        progress?.Invoke(1f);
        return factionProto;
    }

    private void SpawnQuestTargets(
        EntityUid mapUid,
        MapGridComponent grid,
        StargatePlanetPresetPrototype preset,
        Vector2i origin,
        Random random, List<Dungeon> dungeons)
    {
        if (dungeons.Count == 0 || dungeons.All(d => d.Rooms.Count == 0)) return;
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

        structureCount *= 2;
        if (bossCount > 0)
            bossCount *= 2;

        _planetQuest.SetupQuest(mapUid, structureCount, bossCount, questProto.RewardMin, questProto.RewardMax, questProto.RewardMultiplier, questProto.Name, questProto.Description, random);
        if (structureCount > 0 && questProto.StructurePrototypes.Count > 0)
        {
            for (var i = 0; i < structureCount; i++)
            {
                var protoId = questProto.StructurePrototypes[random.Next(questProto.StructurePrototypes.Count)];
                if (!TryFindDungeonSpawnTile(dungeons, mapUid, grid, origin, random, out var tile)) break;
                var uid = SpawnAtPosition(protoId, _maps.GridTileToLocal(mapUid, grid, tile));
                _planetQuest.RegisterTarget(uid, mapUid, PlanetObjectiveType.DestroyStructures);
            }
        }
        if (bossCount > 0 && questProto.BossPrototypes.Count > 0)
        {
            for (var i = 0; i < bossCount; i++)
            {
                var bossProtoId = questProto.BossPrototypes[random.Next(questProto.BossPrototypes.Count)];
                if (!TryFindDungeonSpawnTile(dungeons, mapUid, grid, origin, random, out var tile)) break;
                var uid = SpawnAtPosition(bossProtoId, _maps.GridTileToLocal(mapUid, grid, tile));
                _planetQuest.RegisterTarget(uid, mapUid, PlanetObjectiveType.KillBoss);
            }
        }
    }
    private bool TryFindDungeonSpawnTile(List<Dungeon> dungeons, EntityUid gridUid, MapGridComponent grid, Vector2i origin, Random random, out Vector2i tile)
    {
        tile = default;
        var safeRadiusSq = StargateSafeRadiusTiles * StargateSafeRadiusTiles;
        var availableRooms = dungeons.SelectMany(d => d.Rooms).ToList();
        if (availableRooms.Count == 0) return false;
        var roomAttempts = Math.Min(availableRooms.Count, 40);
        for (var r = 0; r < roomAttempts; r++)
        {
            var room = availableRooms[random.Next(availableRooms.Count)];
            var tiles = room.Tiles.ToList();
            while (tiles.Count > 0)
            {
                var candidate = tiles[random.Next(tiles.Count)];
                tiles.Remove(candidate);
                var dt = candidate - origin;
                if (dt.X * dt.X + dt.Y * dt.Y <= safeRadiusSq) continue;
                if (!_anchorable.TileFree((gridUid, grid), candidate, (int)CollisionGroup.MachineLayer, (int)CollisionGroup.MachineLayer)) continue;
                tile = candidate;
                return true;
            }
        }
        return false;
    }

    public async Task<EntityUid?> CreateExpeditionPlanetAsync(int seed, string? presetIdOverride, string? questIdOverride, int? rewardOverride, CancellationToken cancel = default, Action<float>? progress = null)
    {
        var presetId = presetIdOverride ?? GetExpeditionPresetForSeed(seed);
        if (presetId == null || !_protoManager.TryIndex<StargatePlanetPresetPrototype>(presetId, out var preset)) preset = GetDefaultExpeditionPreset();
        var random = new Random(seed);
        var mapUid = _maps.CreateMap();
        var planetName = _salvage.GetFTLName(_protoManager.Index(preset.NameDataset), seed);
        _metadata.SetEntityName(mapUid, planetName);
        const int MaxOffset = 256;
        var origin = new Vector2i(random.Next(-MaxOffset, MaxOffset), random.Next(-MaxOffset, MaxOffset));
        var worldRadius = preset.WorldRadiusMin + (float)(random.NextDouble() * (preset.WorldRadiusMax - preset.WorldRadiusMin));
        if (preset.RestrictedRange > 0f)
            worldRadius = preset.RestrictedRange;
        AddComp(mapUid, new RestrictedRangeComponent
        {
            Range = worldRadius,
            Origin = origin
        });
        progress?.Invoke(0.12f);
        await YieldPlanetGenTick();
        EnsureComp<ExpeditionPlanetComponent>(mapUid);
        if (TryComp<ExpeditionPlanetComponent>(mapUid, out var expeditionPlanet))
        {
            expeditionPlanet.LandingOrigin = origin;
            Dirty(mapUid, expeditionPlanet);
        }
        var biomeId = preset.Biome[random.Next(preset.Biome.Count)];
        _biome.EnsurePlanet(mapUid, _protoManager.Index(biomeId), seed);
        await YieldPlanetGenTick();
        ApplyEnvironmentMods(mapUid, preset, random);
        progress?.Invoke(0.2f);
        await YieldPlanetGenTick();
        if (!TryComp<MapGridComponent>(mapUid, out var grid)) return mapUid;
        progress?.Invoke(0.28f);
        var dungeons = await GenerateDungeonsAsync(mapUid, grid, preset, biomeId.Id, origin, seed, random, useExpeditionPool: true, progress: p => progress?.Invoke(0.28f + p * 0.45f));
        cancel.ThrowIfCancellationRequested();
        if (!TryComp<MapGridComponent>(mapUid, out grid)) return mapUid;
        progress?.Invoke(0.76f);
        await YieldPlanetGenTick();
        var dungeonFaction = await SpawnBudgetMobsAsync(mapUid, grid, preset, dungeons, origin, random, p => progress?.Invoke(0.76f + p * 0.1f));
        progress?.Invoke(0.88f);
        await YieldPlanetGenTick();
        if (TryComp<BiomeComponent>(mapUid, out var biomeComp))
        {
            AddLootLayers(mapUid, biomeComp, preset, random);
            await YieldPlanetGenTick();
            AddMobLayers(mapUid, biomeComp, preset, random, dungeonFaction, forceDungeonOnly: true);
        }
        progress?.Invoke(0.92f);
        await YieldPlanetGenTick();
        SpawnQuestTargetsExpedition(mapUid, grid, preset, origin, random, dungeons, questIdOverride, rewardOverride);
        progress?.Invoke(0.96f);
        await YieldPlanetGenTick();
        return mapUid;
    }

    private void SpawnQuestTargetsExpedition(EntityUid mapUid, MapGridComponent grid, StargatePlanetPresetPrototype preset, Vector2i origin, Random random, List<Dungeon> dungeons, string? questIdOverride, int? rewardOverride)
    {
        if (dungeons.Count == 0 || dungeons.All(d => d.Rooms.Count == 0)) return;
        PlanetQuestPrototype questProto;
        if (questIdOverride != null && _protoManager.TryIndex<PlanetQuestPrototype>(questIdOverride, out var indexed)) questProto = indexed;
        else
        {
            var questPool = preset.QuestPrototypes.Count > 0 ? preset.QuestPrototypes.Select(id => _protoManager.Index<PlanetQuestPrototype>(id)).ToList() : _protoManager.EnumeratePrototypes<PlanetQuestPrototype>().ToList();
            if (questPool.Count == 0) return;
            questProto = questPool[random.Next(questPool.Count)];
        }
        var structureCount = 0;
        if (questProto.StructureCountMax > 0)
        {
            var min = Math.Max(0, questProto.StructureCountMin);
            var max = Math.Max(min, questProto.StructureCountMax);
            structureCount = random.Next(min, max + 1);
        }
        var bossCount = Math.Max(0, questProto.BossCount);

        structureCount *= 2;
        if (bossCount > 0)
            bossCount *= 2;

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

        if (rewardOverride is { } reward && TryComp<PlanetQuestComponent>(mapUid, out var quest))
        {
            quest.TotalReward = reward;
            Dirty(mapUid, quest);
        }

        if (structureCount > 0 && questProto.StructurePrototypes.Count > 0)
        {
            for (var i = 0; i < structureCount; i++)
            {
                var protoId = questProto.StructurePrototypes[random.Next(questProto.StructurePrototypes.Count)];
                if (!TryFindDungeonSpawnTile(dungeons, mapUid, grid, origin, random, out var tile)) break;
                var uid = SpawnAtPosition(protoId, _maps.GridTileToLocal(mapUid, grid, tile));
                _planetQuest.RegisterTarget(uid, mapUid, PlanetObjectiveType.DestroyStructures);
            }
        }

        if (bossCount > 0 && questProto.BossPrototypes.Count > 0)
        {
            for (var i = 0; i < bossCount; i++)
            {
                var bossProtoId = questProto.BossPrototypes[random.Next(questProto.BossPrototypes.Count)];
                if (!TryFindDungeonSpawnTile(dungeons, mapUid, grid, origin, random, out var tile)) break;
                var uid = SpawnAtPosition(bossProtoId, _maps.GridTileToLocal(mapUid, grid, tile));
                _planetQuest.RegisterTarget(uid, mapUid, PlanetObjectiveType.KillBoss);
            }
        }
    }
    private static int PickWeightedDungeonCount(Random random, StargatePlanetPresetPrototype preset)
    {
        var min = Math.Clamp(preset.DungeonCountMin, 0, 3);
        var max = Math.Clamp(preset.DungeonCountMax, 0, 3);
        if (preset.DungeonCountMax > 3)
        {
            min = Math.Max(0, preset.DungeonCountMin);
            max = preset.DungeonCountMax;
            return random.Next(min, max + 1);
        }
        var totalWeight = 0;
        for (var i = min; i <= max; i++) totalWeight += DungeonCountWeights[i];
        if (totalWeight <= 0) return 0;
        var roll = random.Next(totalWeight);
        var acc = 0;
        for (var i = min; i <= max; i++)
        { acc += DungeonCountWeights[i]; if (roll < acc) return i; }
        return max;
    }

    private List<ProtoId<DungeonConfigPrototype>> BuildConfigPool(Random random, StargatePlanetPresetPrototype preset, string biomeId, bool useExpeditionPool)
    {
        var source = useExpeditionPool ? ResolveExpeditionConfigPool(preset, biomeId) : ExpeditionDungeonPools.StargateLegacy;
        var pool = new List<ProtoId<DungeonConfigPrototype>>();
        foreach (var id in source)
        {
            if (_protoManager.HasIndex<DungeonConfigPrototype>(id))
                pool.Add(id);
        }

        if (pool.Count == 0 && useExpeditionPool)
        {
            foreach (var id in ExpeditionDungeonPools.StargateLegacy)
            {
                if (_protoManager.HasIndex<DungeonConfigPrototype>(id)) pool.Add(id);
            }
        }

        for (var i = pool.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        return pool;
    }

    private static ProtoId<DungeonConfigPrototype>[] ResolveExpeditionConfigPool(StargatePlanetPresetPrototype preset, string biomeId)
    {
        if (biomeId.Contains("Caves")) return ExpeditionDungeonPools.ExpeditionCaves;
        if (biomeId.Contains("Shadow")) return ExpeditionDungeonPools.ExpeditionShadow;
        if (biomeId.Contains("Lava") || biomeId.Contains("Desert") || biomeId.Contains("Snow")) return ExpeditionDungeonPools.ExpeditionExtreme;
        if (preset.Biome.Exists(biome => biome.Id.Contains("Caves"))) return ExpeditionDungeonPools.ExpeditionCaves;
        if (preset.Biome.Exists(biome => biome.Id.Contains("Shadow"))) return ExpeditionDungeonPools.ExpeditionShadow;
        if (preset.Biome.Exists(biome => biome.Id.Contains("Lava") || biome.Id.Contains("Desert") || biome.Id.Contains("Snow"))) return ExpeditionDungeonPools.ExpeditionExtreme;
        return ExpeditionDungeonPools.ExpeditionGrass;
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
        string? dungeonFaction, bool forceDungeonOnly = false)
    {
        var mode = forceDungeonOnly ? MobSpawnMode.DungeonOnly : preset.MobSpawnMode;
        switch (mode)
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
        if (preset.RestrictedRange > 0f)
            worldRadius = preset.RestrictedRange;

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
        var soft = EnsureComp<SoftPlanetOverlayComponent>(mapUid);
        Dirty(mapUid, soft);
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
                if (SoftPlanetOverlayComponent.IsDenseAirMod(airModId)) SoftenMapGasOverlay(mapUid);
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

    private void SoftenMapGasOverlay(EntityUid mapUid)
    {
        if (!TryComp<SoftPlanetOverlayComponent>(mapUid, out var soft)) return;
        _atmosphere.ScaleMapGasOverlay(mapUid, soft.GasOverlayOpacity);
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

    public ExpeditionOfferResolution? ResolveOfferFromSeed(int seed)
    {
        var presetId = GetExpeditionPresetForSeed(seed);
        if (presetId == null || !_protoManager.TryIndex<StargatePlanetPresetPrototype>(presetId, out var preset)) preset = GetDefaultExpeditionPreset();
        var random = new Random(seed);
        const int MaxOffset = 256; _ = random.Next(-MaxOffset, MaxOffset); _ = random.Next(-MaxOffset, MaxOffset); _ = random.NextDouble();
        var biomeId = preset.Biome[random.Next(preset.Biome.Count)].Id;
        if (preset.TemperatureMods is { Count: > 0 }) _ = random.Next(preset.TemperatureMods.Count);
        var airDescription = Loc.GetString("expedition-air-unknown");
        if (preset.AirMods is { Count: > 0 })
        {
            var airModId = preset.AirMods[random.Next(preset.AirMods.Count)];
            if (_protoManager.TryIndex(airModId, out var airMod)) airDescription = !string.IsNullOrEmpty(airMod.Description) ? Loc.GetString(airMod.Description) : airMod.ID;
        }

        if (preset.LightMods is { Count: > 0 }) _ = random.Next(preset.LightMods.Count);
        var weatherDescription = Loc.GetString("expedition-weather-none");
        if (preset.WeatherMods is { Count: > 0 })
        {
            var weatherModId = preset.WeatherMods[random.Next(preset.WeatherMods.Count)];
            if (_protoManager.TryIndex(weatherModId, out var weatherMod))
            { weatherDescription = !string.IsNullOrEmpty(weatherMod.Description) ? Loc.GetString(weatherMod.Description) : weatherMod.ID; }
        }

        PlanetQuestPrototype questProto;
        if (preset.QuestPrototypes.Count > 0)
        { questProto = _protoManager.Index(preset.QuestPrototypes[random.Next(preset.QuestPrototypes.Count)]); }
        else
        {
            var questPool = _protoManager.EnumeratePrototypes<PlanetQuestPrototype>().ToList();
            if (questPool.Count == 0) return null;
            questProto = questPool[random.Next(questPool.Count)];
        }
        var baseReward = random.Next(questProto.RewardMin, questProto.RewardMax + 1);
        var reward = (int) (baseReward * questProto.RewardMultiplier);
        var planetName = _salvage.GetFTLName(_protoManager.Index(preset.NameDataset), seed);
        return new ExpeditionOfferResolution(preset.ID, questProto.ID, planetName, biomeId, airDescription, weatherDescription, reward);
    }

    public string? GetPresetForSeed(int seed)
    { return PickWeightedPresetId(seed, expeditionOnly: false); }
    public string? GetExpeditionPresetForSeed(int seed)
    { return PickWeightedPresetId(seed, expeditionOnly: true); }

    private string? PickWeightedPresetId(int seed, bool expeditionOnly)
    {
        var presets = new List<(string Id, float Weight)>();
        foreach (var proto in _protoManager.EnumeratePrototypes<StargatePlanetPresetPrototype>())
        {
            if (expeditionOnly && !proto.ID.StartsWith("ExpPreset", StringComparison.Ordinal)) continue;
            presets.Add((proto.ID, proto.Weight));
        }

        if (presets.Count == 0)
            return null;

        var random = new Random(seed);
        var totalWeight = 0f;
        foreach (var (_, w) in presets)
            totalWeight += w;

        var roll = (float) (random.NextDouble() * totalWeight);
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

    private StargatePlanetPresetPrototype GetDefaultExpeditionPreset()
    {
        if (_protoManager.TryIndex<StargatePlanetPresetPrototype>("ExpPresetGrasslandRaid", out var preset)) return preset;
        return GetDefaultPreset();
    }
}

public sealed record ExpeditionOfferResolution(string PresetId, string QuestId, string PlanetName, string BiomeId, string AirDescription, string WeatherDescription, int Reward);
