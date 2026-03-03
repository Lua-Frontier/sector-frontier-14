// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.GameStates;

namespace Content.Shared._Lua.Stargate.PlanetQuest;

public abstract class SharedPlanetQuestSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlanetQuestComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<PlanetQuestComponent, ComponentHandleState>(OnHandleState);
    }

    private void OnGetState(EntityUid uid, PlanetQuestComponent comp, ref ComponentGetState args)
    {
        args.State = new PlanetQuestComponentState
        {
            QuestName = comp.QuestName,
            QuestDescription = comp.QuestDescription,
            StructureTotalCount = comp.StructureTotalCount,
            StructureCompletedCount = comp.StructureCompletedCount,
            BossTotalCount = comp.BossTotalCount,
            BossCompletedCount = comp.BossCompletedCount,
            TotalReward = comp.TotalReward,
            ActivePlayerCount = comp.ActivePlayerCount,
            Completed = comp.Completed,
        };
    }

    private void OnHandleState(EntityUid uid, PlanetQuestComponent comp, ref ComponentHandleState args)
    {
        if (args.Current is not PlanetQuestComponentState state)
            return;

        comp.QuestName = state.QuestName;
        comp.QuestDescription = state.QuestDescription;
        comp.StructureTotalCount = state.StructureTotalCount;
        comp.StructureCompletedCount = state.StructureCompletedCount;
        comp.BossTotalCount = state.BossTotalCount;
        comp.BossCompletedCount = state.BossCompletedCount;
        comp.TotalReward = state.TotalReward;
        comp.ActivePlayerCount = state.ActivePlayerCount;
        comp.Completed = state.Completed;
    }
}
