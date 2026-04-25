// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Stargate.PlanetQuest;

[RegisterComponent, NetworkedComponent]
public sealed partial class PlanetQuestComponent : Component
{
    [DataField]
    public string QuestName = string.Empty;

    [DataField]
    public string QuestDescription = string.Empty;

    [DataField]
    public int StructureTotalCount;

    [DataField]
    public int StructureCompletedCount;

    [DataField]
    public int BossTotalCount;

    [DataField]
    public int BossCompletedCount;

    [DataField]
    public int TotalReward;

    [DataField]
    public int ActivePlayerCount;

    [DataField]
    public bool Completed;
}

[Serializable, NetSerializable]
public sealed class PlanetQuestComponentState : ComponentState
{
    public string QuestName = string.Empty;
    public string QuestDescription = string.Empty;

    public int StructureTotalCount;
    public int StructureCompletedCount;
    public int BossTotalCount;
    public int BossCompletedCount;
    public int TotalReward;
    public int ActivePlayerCount;
    public bool Completed;
}

public enum PlanetObjectiveType : byte
{
    DestroyStructures,
    KillBoss
}
