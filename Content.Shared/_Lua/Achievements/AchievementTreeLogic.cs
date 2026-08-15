// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Linq;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lua.Achievements;

public enum AchievementNodeState : byte
{
    Hidden,
    Locked,
    Available,
    Unlocked,
}

public static class AchievementTreeLogic
{
    public static AchievementNodeState GetState(
        AchievementPrototype proto,
        HashSet<string> unlocked)
    {
        if (unlocked.Contains(proto.ID))
            return AchievementNodeState.Unlocked;

        if (!IsVisible(proto, unlocked))
            return AchievementNodeState.Hidden;

        return ArePrerequisitesMet(proto, unlocked)
            ? AchievementNodeState.Available
            : AchievementNodeState.Locked;
    }

    public static bool IsVisible(AchievementPrototype proto, HashSet<string> unlocked)
    {
        if (proto.Disabled)
            return false;

        if (proto.Hidden && !unlocked.Contains(proto.ID))
            return false;
        if (proto.Prerequisites.Count == 0)
            return true;
        if (unlocked.Contains(proto.ID))
            return true;
        return proto.Prerequisites.Any(p => unlocked.Contains(p));
    }

    public static bool ArePrerequisitesMet(AchievementPrototype proto, HashSet<string> unlocked)
    {
        if (proto.Disabled)
            return false;

        return proto.Prerequisites.Count == 0 ||
               proto.Prerequisites.All(p => unlocked.Contains(p));
    }

    public static int GetDepth(
        AchievementPrototype proto,
        IPrototypeManager prototypes,
        Dictionary<string, int>? cache = null)
    {
        cache ??= new Dictionary<string, int>();
        if (cache.TryGetValue(proto.ID, out var depth))
            return depth;

        if (proto.Prerequisites.Count == 0)
        {
            cache[proto.ID] = 0;
            return 0;
        }

        var max = 0;
        foreach (var prereq in proto.Prerequisites)
        {
            if (!prototypes.TryIndex(prereq, out AchievementPrototype? parent))
                continue;

            max = Math.Max(max, GetDepth(parent, prototypes, cache) + 1);
        }

        cache[proto.ID] = max;
        return max;
    }
}
