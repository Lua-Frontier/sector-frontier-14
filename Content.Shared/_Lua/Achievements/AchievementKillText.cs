// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Prototypes;

namespace Content.Shared._Lua.Achievements;

public static class AchievementKillText
{
    public static string GetDescription(AchievementPrototype proto, IPrototypeManager prototypes)
    {
        if (!proto.IsKillAchievement || proto.RequiredKillCount <= 1)
            return Loc.GetString(proto.Description);

        return Loc.GetString(proto.Description, ("count", proto.RequiredKillCount));
    }
}
