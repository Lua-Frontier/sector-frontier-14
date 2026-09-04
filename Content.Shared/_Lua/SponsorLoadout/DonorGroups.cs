// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using System.Collections.Generic;

namespace Content.Shared._Lua.SponsorLoadout;

public static class DonorGroups
{
    public const string Shareholder = "Shareholder";
    public const string ShareholderLua = "ShareholderLua";
    public const string God = "God";
    public const string Rank1 = "Rank1";
    public const string Rank2 = "Rank2";
    public const string Rank3 = "Rank3";
    public const string Rank4 = "Rank4";
    public const string Rank5 = "Rank5";
    public const string Rank6 = "Rank6";
    public const string Rank7 = "Rank7";
    public const string Rank8 = "Rank8";
    public const string Rank9 = "Rank9";
    public const string Rank10 = "Rank10";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Shareholder,
        ShareholderLua,
        God,
        Rank1,
        Rank2,
        Rank3,
        Rank4,
        Rank5,
        Rank6,
        Rank7,
        Rank8,
        Rank9,
        Rank10,
    };

    public static bool IsKnownTier(string? role)
    {
        return TryResolveTier(role, out _);
    }

    public static bool TryResolveTier(string? value, out string tier)
    {
        tier = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        foreach (var known in All)
        {
            if (!string.Equals(known, trimmed, StringComparison.OrdinalIgnoreCase))
                continue;
            tier = known;
            return true;
        }

        tier = trimmed.ToLowerInvariant() switch
        {
            "акционер" => Shareholder,
            "божество" => God,
            "ранг i" => Rank1,
            "ранг ii" => Rank2,
            "ранг iii" => Rank3,
            "ранг iv" => Rank4,
            "ранг v" => Rank5,
            "ранг vi" => Rank6,
            "ранг vii" => Rank7,
            "ранг viii" => Rank8,
            "ранг ix" => Rank9,
            "ранг x" => Rank10,
            _ => string.Empty
        };

        return tier.Length > 0;
    }

    public static HashSet<string> GetEffectiveTiers(IEnumerable<string> roles)
    {
        var effective = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawRole in roles)
        {
            if (string.IsNullOrWhiteSpace(rawRole))
                continue;

            if (!TryResolveTier(rawRole, out var role))
                continue;
            switch (role)
            {
                case Shareholder:
                    effective.Add(Shareholder);
                    break;
                case ShareholderLua:
                    effective.Add(ShareholderLua);
                    break;
                case God:
                    effective.Add(God);
                    break;
                case Rank1:
                    AddRanks(effective, Rank1, Rank4, Rank5, Rank6, Rank7, Rank8, Rank9, Rank10);
                    break;
                case Rank2:
                    AddRanks(effective, Rank2, Rank4, Rank5, Rank6, Rank7, Rank8, Rank9, Rank10);
                    break;
                case Rank3:
                    AddRanks(effective, Rank3, Rank4, Rank5, Rank6, Rank7, Rank8, Rank9, Rank10);
                    break;
                case Rank4:
                    AddRanks(effective, Rank4, Rank5, Rank6, Rank7, Rank8, Rank9, Rank10);
                    break;
                case Rank5:
                    AddRanks(effective, Rank5, Rank6, Rank7, Rank8, Rank9, Rank10);
                    break;
                case Rank6:
                    AddRanks(effective, Rank6, Rank7, Rank8, Rank9, Rank10);
                    break;
                case Rank7:
                    AddRanks(effective, Rank7, Rank8, Rank9, Rank10);
                    break;
                case Rank8:
                    AddRanks(effective, Rank8, Rank9, Rank10);
                    break;
                case Rank9:
                    AddRanks(effective, Rank9, Rank10);
                    break;
                case Rank10:
                    effective.Add(Rank10);
                    break;
            }
        }

        return effective;
    }

    private static void AddRanks(HashSet<string> tiers, params string[] ranks)
    {
        tiers.UnionWith(ranks);
    }
    public static List<string> GetShopHeaderTokens(IEnumerable<string> roles)
    {
        var owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in roles)
        {
            if (TryResolveTier(raw, out var tier))
                owned.Add(tier);
        }

        var tokens = new List<string>();
        if (owned.Contains(Shareholder) || owned.Contains(ShareholderLua))
            tokens.Add(Shareholder);
        if (owned.Contains(God))
            tokens.Add(God);
        foreach (var rank in RankShopCategoryOrder)
        {
            if (owned.Contains(rank))
                tokens.Add(rank);
        }
        return tokens;
    }

    public static IReadOnlyList<string> RankShopCategoryOrder { get; } =
    [
        Rank1, Rank2, Rank3, Rank4, Rank5, Rank6, Rank7, Rank8, Rank9, Rank10
    ];
}


