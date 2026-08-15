// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

namespace Content.Shared._Lua.Achievements;

public static class AchievementPlaytimeTiers
{
    public static readonly (int Hours, string Id)[] All =
    {
        (1, AchievementIds.Playtime1H),
        (2, AchievementIds.Playtime2H),
        (4, AchievementIds.Playtime4H),
        (8, AchievementIds.Playtime8H),
        (16, AchievementIds.Playtime16H),
        (32, AchievementIds.Playtime32H),
        (64, AchievementIds.Playtime64H),
        (128, AchievementIds.Playtime128H),
        (256, AchievementIds.Playtime256H),
        (512, AchievementIds.Playtime512H),
        (1000, AchievementIds.Playtime1000H),
    };
}
