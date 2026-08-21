// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Prototypes;

namespace Content.Shared._Lua.Achievements;

[DataDefinition]
public sealed partial record AchievementItemReward
{
    [DataField("id", required: true)]
    public EntProtoId Prototype;

    [DataField]
    public int Count = 1;
}
