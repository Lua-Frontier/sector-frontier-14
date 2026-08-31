// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.
using Content.Shared._NF.Bank;
using System.Numerics;
using Content.Server.Cargo.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Content.Server.Shuttles.Systems;
using Content.Server._Lua.Shuttles.Systems;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Random;
using Content.Server._NF.Bank;
using Content.Shared._NF.Bank.BUI;
using Content.Server.Procedural;
using Robust.Shared.Prototypes;
using Content.Server.Maps.NameGenerators;
using Content.Server.StationEvents.Events;
using Content.Server._NF.Station.Systems;
using Robust.Shared.EntitySerialization.Systems;
using Content.Server._Lua.Sectors;

namespace Content.Server._Lua.AiShuttle;

public sealed class AiShuttleSpawnRule : StationEventSystem<AiShuttleSpawnRuleComponent>
{
    NanotrasenNameGenerator _nameGenerator = new();
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MapLoaderSystem _loader = default!;
    [Dependency] private readonly MetaDataSystem _metadata = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;
    [Dependency] private readonly StationRenameWarpsSystems _renameWarps = default!;
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly SectorSystem _sectors = default!;
    [Dependency] private readonly ShuttleGridAccessSystem _gridAccess = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    private readonly Dictionary<EntityUid, MapId> _eventMap = new();
    private MapId _relevantMapId = MapId.Nullspace;

    protected override MapId GetRelevantMapId()
    {
        return _relevantMapId;
    }

    protected override void Added(EntityUid uid, AiShuttleSpawnRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        if (!TryResolveTargetMap(uid, component, out var targetMapId))
        {
            _eventMap[uid] = MapId.Nullspace;
            _relevantMapId = MapId.Nullspace;
            Log.Error($"AiShuttleSpawnRule: no target map for {ToPrettyString(uid)}; event will not spawn");
            return;
        }

        _eventMap[uid] = targetMapId;
        _relevantMapId = targetMapId;
        base.Added(uid, component, gameRule, args);
    }

    protected override void Started(EntityUid uid, AiShuttleSpawnRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!_eventMap.TryGetValue(uid, out var targetMapId) ||
            targetMapId == MapId.Nullspace ||
            !_map.MapExists(targetMapId))
        {
            Log.Error($"AiShuttleSpawnRule: aborting {ToPrettyString(uid)} — target map missing");
            return;
        }

        var mapUid = _mapManager.GetMapEntityId(targetMapId);
        _relevantMapId = targetMapId;
        var spawnCoords = new EntityCoordinates(mapUid, Vector2.Zero);
        _map.CreateMap(out var mapId);
        foreach (var group in component.Groups.Values)
        {
            var count = _random.Next(group.MinCount, group.MaxCount + 1);
            for (var i = 0; i < count; i++)
            {
                EntityUid spawned;
                if (group.MinimumDistance > 0f)
                { spawnCoords = spawnCoords.WithPosition(_random.NextVector2(group.MinimumDistance, group.MaximumDistance)); }
                switch (group)
                {
                    case AiShuttleDungeonSpawnGroup dungeon: if (!TryDungeonSpawn(spawnCoords, component, ref dungeon, i, out spawned)) continue; break;
                    case AiShuttleGridSpawnGroup grid: if (!TryGridSpawn(spawnCoords, uid, mapId, ref grid, i, out spawned)) continue; break;
                    default: throw new NotImplementedException();
                }
                if (group.NameLoc != null && group.NameLoc.Count > 0) { _metadata.SetEntityName(spawned, Loc.GetString(_random.Pick(group.NameLoc))); }
                else if (_protoManager.TryIndex(group.NameDataset, out var dataset))
                {
                    string gridName;
                    switch (group.NameDatasetType)
                    {
                        case AiShuttleDatasetNameType.FTL: gridName = _nameGenerator.FormatName(Loc.GetString(_random.Pick(dataset.Values)) + " {1}"); break;
                        case AiShuttleDatasetNameType.Nanotrasen: gridName = _nameGenerator.FormatName(Loc.GetString(_random.Pick(dataset.Values)) + " {1}"); break;
                        case AiShuttleDatasetNameType.Verbatim:
                        default: gridName = Loc.GetString(_random.Pick(dataset.Values)); break;
                    }
                    _metadata.SetEntityName(spawned, gridName);
                }
                if (group.NameWarp)
                {
                    bool? adminOnly = group.HideWarp ? true : null;
                    _renameWarps.SyncWarpPointsToGrid(spawned, forceAdminOnly: adminOnly);
                }
                EntityManager.AddComponents(spawned, group.AddComponents);
                component.GridsUid.Add(spawned);
            }
        }
        _map.DeleteMap(mapId);
    }

    private bool TryDungeonSpawn(EntityCoordinates spawnCoords, AiShuttleSpawnRuleComponent component, ref AiShuttleDungeonSpawnGroup group, int i, out EntityUid spawned)
    {
        spawned = EntityUid.Invalid;
        if (group.Protos.Count <= 0) return false;
        int maxIndex = group.Protos.Count - (i % group.Protos.Count);
        int index = _random.Next(maxIndex);
        var dungeonProtoId = group.Protos[index];
        group.Protos.RemoveAt(index);
        group.Protos.Add(dungeonProtoId);
        if (!_protoManager.TryIndex(dungeonProtoId, out var dungeonProto)) return false;
        _mapSystem.CreateMap(out var mapId);
        var spawnedGrid = _mapManager.CreateGridEntity(mapId);
        _transform.SetMapCoordinates(spawnedGrid, new MapCoordinates(Vector2.Zero, mapId));
        var dungeonSystem = Get<DungeonSystem>();
        dungeonSystem.GenerateDungeon(dungeonProto, dungeonProtoId, spawnedGrid.Owner, spawnedGrid.Comp, Vector2i.Zero, _random.Next(), spawnCoords);
        spawned = spawnedGrid.Owner;
        component.MapsUid.Add(mapId);
        return true;
    }

    private bool TryGridSpawn(EntityCoordinates spawnCoords, EntityUid stationUid, MapId mapId, ref AiShuttleGridSpawnGroup group, int i, out EntityUid spawned)
    {
        spawned = EntityUid.Invalid;
        if (group.Paths.Count == 0) return false;
        int maxIndex = group.Paths.Count - (i % group.Paths.Count);
        int index = _random.Next(maxIndex);
        var path = group.Paths[index];
        group.Paths.RemoveAt(index);
        group.Paths.Add(path);
        if (_loader.TryLoadGrid(mapId, path, out var ent))
        {
            _gridAccess.EnsureGridType(ent.Value.Owner, ShuttleGridKind.ShuttleAi);
            if (_gridAccess.TryGetShuttleGrid(ent.Value.Owner, out _))
                _shuttle.TryFTLProximity(ent.Value.Owner, spawnCoords);
            if (group.NameGrid)
            {
                string name;
                if (TryComp<MetaDataComponent>(ent.Value, out var metaData) && !string.IsNullOrWhiteSpace(metaData.EntityName))
                { name = metaData.EntityName; }
                else
                { name = path.FilenameWithoutExtension; }
                _metadata.SetEntityName(ent.Value, name);
            }
            spawned = ent.Value;
            return true;
        }
        return false;
    }

    private bool TryResolveTargetMap(EntityUid uid, AiShuttleSpawnRuleComponent component, out MapId targetMapId)
    {
        if (string.IsNullOrEmpty(component.SectorStarmap))
        {
            Log.Error($"AiShuttleSpawnRule: sectorStarmap is required; aborting {ToPrettyString(uid)}");
            targetMapId = MapId.Nullspace;
            return false;
        }

        if (_sectors.TryGetMapId(component.SectorStarmap, out targetMapId) &&
            targetMapId != MapId.Nullspace &&
            _map.MapExists(targetMapId))
            return true;

        Log.Error($"AiShuttleSpawnRule: sectorStarmap '{component.SectorStarmap}' is not loaded; aborting {ToPrettyString(uid)}");
        targetMapId = MapId.Nullspace;
        return false;
    }

    protected override void Ended(EntityUid uid, AiShuttleSpawnRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        if (_eventMap.TryGetValue(uid, out var eventMap))
            _relevantMapId = eventMap;

        base.Ended(uid, component, gameRule, args);
        _eventMap.Remove(uid);
        _relevantMapId = MapId.Nullspace;

        if (component.GridsUid == null) return;
        foreach (var componentGridUid in component.GridsUid)
        {
            if (!EntityManager.TryGetComponent<TransformComponent>(componentGridUid, out var gridTransform)) return;
            if (gridTransform.GridUid is not EntityUid gridUid) return;
            if (component.DeleteGridsOnEnd)
            {
                var bankContext = gridTransform.MapUid;
                var gridValue = _pricing.AppraiseGrid(gridUid, null);
                Del(gridUid);
                foreach (var (account, rewardCoeff) in component.RewardAccounts)
                {
                    var reward = (int)(gridValue * rewardCoeff);
                    _bank.TrySectorDeposit(account, reward, LedgerEntryType.BluespaceReward, bankContext);
                }
            }
        }
        foreach (MapId mapId in component.MapsUid) { if (_map.MapExists(mapId)) _map.DeleteMap(mapId); }
    }
}
