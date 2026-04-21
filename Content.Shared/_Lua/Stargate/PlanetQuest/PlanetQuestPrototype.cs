// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.Prototypes;

namespace Content.Shared._Lua.Stargate.PlanetQuest;

[Prototype]
public sealed partial class PlanetQuestPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    [DataField] public LocId Name = string.Empty;

    [DataField] public LocId Description = string.Empty;

    [DataField] public int StructureCountMin;

    [DataField] public int StructureCountMax;

    [DataField] public int BossCount;

    [DataField] public int RewardMin = 20000;

    [DataField] public int RewardMax = 80000;

    [DataField] public float RewardMultiplier = 1.0f;

    [DataField] public List<EntProtoId> StructurePrototypes = new();

    [DataField] public List<EntProtoId> BossPrototypes = new();
}

