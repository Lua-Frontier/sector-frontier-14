// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using System.Collections.Generic;
using System.Linq;

namespace Content.Lua.Shared.SponsorLoadout;

public static class DonorGroups
{
    public const string Shareholder = "Shareholder";
    public const string ShareholderLua = "ShareholderLua";
    public const string God = "God";
    public const string Boost = "Boost";
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
        Boost,
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
            "boost" => Boost,
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
                case Boost:
                    effective.Add(Boost);
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
        if (owned.Contains(Boost))
            tokens.Add(Boost);
        return tokens;
    }

    public static IReadOnlyList<string> RankShopCategoryOrder { get; } =
    [
        Rank1, Rank2, Rank3, Rank4, Rank5, Rank6, Rank7, Rank8, Rank9, Rank10
    ];

    public static string? GetTierLocKey(string? tierName)
    {
        if (!TryResolveTier(tierName, out var tier))
            return null;

        return tier switch
        {
            Shareholder => "store-vip-tier-shareholder",
            ShareholderLua => "store-vip-tier-shareholderlua",
            God => "store-vip-tier-god",
            Boost => "store-vip-tier-boost",
            Rank1 => "store-vip-tier-rank1",
            Rank2 => "store-vip-tier-rank2",
            Rank3 => "store-vip-tier-rank3",
            Rank4 => "store-vip-tier-rank4",
            Rank5 => "store-vip-tier-rank5",
            Rank6 => "store-vip-tier-rank6",
            Rank7 => "store-vip-tier-rank7",
            Rank8 => "store-vip-tier-rank8",
            Rank9 => "store-vip-tier-rank9",
            Rank10 => "store-vip-tier-rank10",
            _ => null,
        };
    }

    public static string GetTierDisplayName(string? tierName)
    {
        var locKey = GetTierLocKey(tierName);
        return locKey is null
            ? (string.IsNullOrWhiteSpace(tierName) ? string.Empty : tierName)
            : Loc.GetString(locKey);
    }

    public static int GetTierPriority(string? role)
    {
        if (!TryResolveTier(role, out var tier))
            return 0;

        return tier switch
        {
            Shareholder => 1300,
            ShareholderLua => 1200,
            God => 1100,
            Rank1 => 1000,
            Rank2 => 900,
            Rank3 => 800,
            Rank4 => 700,
            Rank5 => 600,
            Rank6 => 500,
            Rank7 => 400,
            Rank8 => 300,
            Rank9 => 200,
            Rank10 => 100,
            Boost => 50,
            _ => 0
        };
    }

    public static string? GetOocColorHex(string? role)
    {
        if (!TryResolveTier(role, out var tier))
            return null;

        return tier switch
        {
            Shareholder or ShareholderLua => "#F05C29",
            God => "#00FF4A",
            Boost => "#FF4CF1",
            _ => null
        };
    }

    public static int GetOocColorPriority(string? role)
    {
        if (!TryResolveTier(role, out var tier))
            return 0;

        return tier switch
        {
            God => 300,
            Shareholder or ShareholderLua => 200,
            Boost => 100,
            _ => 0
        };
    }

    public static string? SelectHighestOocColorHex(IEnumerable<string> roles)
    {
        string? bestHex = null;
        var bestPriority = int.MinValue;
        foreach (var raw in roles)
        {
            var hex = GetOocColorHex(raw);
            if (hex == null)
                continue;
            var priority = GetOocColorPriority(raw);
            if (priority <= bestPriority)
                continue;
            bestHex = hex;
            bestPriority = priority;
        }

        return bestHex;
    }

    public static string? SelectHighestPriorityRole(IEnumerable<string> roles)
    {
        string? best = null;
        var bestPriority = int.MinValue;
        foreach (var raw in roles)
        {
            if (!TryResolveTier(raw, out var tier))
                continue;
            var priority = GetTierPriority(tier);
            if (priority < bestPriority)
                continue;
            if (priority == bestPriority && best != null)
                continue;
            best = tier;
            bestPriority = priority;
        }

        return best;
    }

    public static string FormatTiersDisplay(IEnumerable<string> roles)
    {
        var tokens = GetShopHeaderTokens(roles);
        if (tokens.Count == 0)
            return string.Empty;

        return string.Join(", ", tokens.Select(GetTierDisplayName));
    }
}


