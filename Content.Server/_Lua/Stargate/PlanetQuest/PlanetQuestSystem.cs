// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server._NF.Bank;
using Content.Server.Popups;
using Content.Shared._Lua.Stargate.PlanetQuest;
using Content.Shared.Ghost;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Server.Player;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;
using System.Linq;

namespace Content.Server._Lua.Stargate.PlanetQuest;

public sealed class PlanetQuestSystem : SharedPlanetQuestSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    private readonly HashSet<MapId> _activePlanetMaps = new();
    private readonly Dictionary<MapId, HashSet<ICommonSession>> _playersOnPlanetMap = new();

    private TimeSpan _nextPlayerScan;
    private static readonly TimeSpan PlayerScanInterval = TimeSpan.FromSeconds(2);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlanetQuestTargetComponent, EntityTerminatingEvent>(OnTargetTerminating);
        SubscribeLocalEvent<PlanetQuestTargetComponent, MobStateChangedEvent>(OnTargetMobStateChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime >= _nextPlayerScan)
        {
            _nextPlayerScan = _timing.CurTime + PlayerScanInterval;
            ScanPlayers();
        }
    }

    private void ScanPlayers()
    {
        _activePlanetMaps.Clear();
        _playersOnPlanetMap.Clear();

        var planetMaps = new HashSet<MapId>();
        var questQuery = EntityQueryEnumerator<PlanetQuestComponent, TransformComponent>();
        while (questQuery.MoveNext(out _, out _, out var xform))
        {
            planetMaps.Add(xform.MapID);
        }

        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is not { } ent || !TryComp<TransformComponent>(ent, out var xform))
                continue;

            var mapId = xform.MapID;
            if (mapId == MapId.Nullspace || !planetMaps.Contains(mapId))
                continue;

            _activePlanetMaps.Add(mapId);

            if (HasComp<GhostComponent>(ent))
                continue;

            if (!_playersOnPlanetMap.TryGetValue(mapId, out var set))
            {
                set = new HashSet<ICommonSession>();
                _playersOnPlanetMap[mapId] = set;
            }
            set.Add(session);
        }

        var updateQuery = EntityQueryEnumerator<PlanetQuestComponent, TransformComponent>();
        while (updateQuery.MoveNext(out var uid, out var quest, out var xform))
        {
            if (quest.Completed)
                continue;

            var count = _playersOnPlanetMap.TryGetValue(xform.MapID, out var players) ? players.Count : 0;
            if (quest.ActivePlayerCount == count)
                continue;

            quest.ActivePlayerCount = count;
            Dirty(uid, quest);
        }
    }

    private void OnTargetTerminating(Entity<PlanetQuestTargetComponent> ent, ref EntityTerminatingEvent args)
    {
        CompleteTarget(ent.Comp);
    }

    private void OnTargetMobStateChanged(Entity<PlanetQuestTargetComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            CompleteTarget(ent.Comp);
    }

    private void CompleteTarget(PlanetQuestTargetComponent target)
    {
        if (!TryComp<PlanetQuestComponent>(target.QuestMap, out var quest) || quest.Completed)
            return;

        if (target.ObjectiveType == PlanetObjectiveType.DestroyStructures)
        {
            if (quest.StructureCompletedCount < quest.StructureTotalCount)
                quest.StructureCompletedCount++;
        }
        else if (target.ObjectiveType == PlanetObjectiveType.KillBoss)
        {
            if (quest.BossCompletedCount < quest.BossTotalCount)
                quest.BossCompletedCount++;
        }

        var allDone = quest.StructureCompletedCount >= quest.StructureTotalCount
                      && quest.BossCompletedCount >= quest.BossTotalCount;

        if (allDone)
        {
            quest.Completed = true;
            DistributeRewards(target.QuestMap, quest);
        }

        Dirty(target.QuestMap, quest);
    }

    private void DistributeRewards(EntityUid mapUid, PlanetQuestComponent quest)
    {
        if (quest.TotalReward <= 0)
            return;

        if (!TryComp<TransformComponent>(mapUid, out var questXform))
            return;

        var mapId = questXform.MapID;
        if (!_playersOnPlanetMap.TryGetValue(mapId, out var sessions) || sessions.Count == 0)
            return;

        var playerList = sessions.ToList();
        var perPlayer = quest.TotalReward / playerList.Count;
        var remainder = quest.TotalReward - perPlayer * playerList.Count;

        for (var i = 0; i < playerList.Count; i++)
        {
            var session = playerList[i];
            if (session.AttachedEntity is not { } playerEnt)
                continue;

            var amount = perPlayer;
            if (i == playerList.Count - 1)
                amount += remainder;

            if (_bank.TryBankDeposit(playerEnt, amount))
            {
                var msg = Loc.GetString("planet-quest-reward-received", ("amount", amount));
                _popup.PopupEntity(msg, playerEnt, Filter.Entities(playerEnt), true, PopupType.Small);
            }
        }
    }

    public void SetupQuest(
        EntityUid mapUid,
        int structureCount,
        int bossCount,
        int rewardMin,
        int rewardMax,
        float rewardMultiplier,
        string questName,
        string questDescription,
        Random random)
    {
        var quest = EnsureComp<PlanetQuestComponent>(mapUid);
        var baseReward = random.Next(rewardMin, rewardMax + 1);
        quest.TotalReward = (int)(baseReward * rewardMultiplier);
        quest.Completed = false;
        quest.QuestName = questName;
        quest.QuestDescription = questDescription;

        quest.StructureTotalCount = structureCount;
        quest.StructureCompletedCount = 0;
        quest.BossTotalCount = bossCount;
        quest.BossCompletedCount = 0;

        Dirty(mapUid, quest);
    }

    public void RegisterTarget(EntityUid targetUid, EntityUid mapUid, PlanetObjectiveType type)
    {
        var comp = EnsureComp<PlanetQuestTargetComponent>(targetUid);
        comp.ObjectiveType = type;
        comp.QuestMap = mapUid;
    }
}
