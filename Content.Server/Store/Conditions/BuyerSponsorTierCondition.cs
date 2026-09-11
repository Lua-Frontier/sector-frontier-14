// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server.Sponsors;
using Content.Shared._Lua.SponsorLoadout;
using Content.Shared.Mind;
using Content.Shared.Store;
using Robust.Shared.IoC;
using System.Linq;

namespace Content.Server.Store.Conditions;

public sealed partial class BuyerSponsorTierCondition : ListingCondition
{
    [DataField("whitelist")]
    public HashSet<string>? Whitelist;
    [DataField("blacklist")]
    public HashSet<string>? Blacklist;

    public override bool Condition(ListingConditionArgs args)
    {
        if (!args.EntityManager.TryGetComponent<MindComponent>(args.Buyer, out var mind)) return false;
        if (mind.UserId is not { } userId) return false;
        var sponsorManager = IoCManager.Resolve<SponsorManager>();
        IEnumerable<string> roles;
        if (sponsorManager.TryGetAllActiveSponsors(userId, out var allSponsors)) roles = allSponsors.Select(s => s.Role).Where(DonorGroups.IsKnownTier);
        else if (sponsorManager.TryGetActiveSponsor(userId, out var sponsor) && DonorGroups.IsKnownTier(sponsor.Role)) roles = [sponsor.Role];
        else return false;
        var roleSet = DonorGroups.GetEffectiveTiers(roles);
        if (roleSet.Count == 0) return false;
        if (Blacklist != null)
        {
            var blacklist = ResolveConfiguredTiers(Blacklist);
            if (roleSet.Overlaps(blacklist)) return false;
        }
        if (Whitelist != null)
        {
            var whitelist = ResolveConfiguredTiers(Whitelist);
            if (!roleSet.Overlaps(whitelist)) return false;
        }
        return true;
    }

    private static HashSet<string> ResolveConfiguredTiers(IEnumerable<string> configured)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in configured)
        {
            if (DonorGroups.TryResolveTier(value, out var tier))
                result.Add(tier);
        }
        return result;
    }
}
