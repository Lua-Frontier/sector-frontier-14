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
        var roleSet = roles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (roleSet.Count == 0) return false;
        if (Blacklist != null && roleSet.Any(r => Blacklist.Contains(r))) return false;
        if (Whitelist != null && !roleSet.Any(r => Whitelist.Contains(r))) return false;
        return true;
    }
}
