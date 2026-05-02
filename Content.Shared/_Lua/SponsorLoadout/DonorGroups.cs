// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using System.Collections.Generic;

namespace Content.Shared._Lua.SponsorLoadout;

public static class DonorGroups
{
    public const string Shareholder = "Акционер";
    public const string God = "Божество";
    public const string Rank1 = "Ранг I";
    public const string Rank2 = "Ранг II";
    public const string Rank3 = "Ранг III";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Shareholder,
        God,
        Rank1,
        Rank2,
        Rank3
    };

    public static bool IsKnownTier(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return false;
        return All.Contains(role.Trim());
    }
}


