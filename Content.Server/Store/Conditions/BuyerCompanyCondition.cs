// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Mono.Company;
using Content.Shared.Mind;
using Content.Shared.Store;

namespace Content.Server.Store.Conditions;

public sealed partial class BuyerCompanyCondition : ListingCondition
{
    [DataField("whitelist")]
    public HashSet<string>? Whitelist;
    [DataField("blacklist")]
    public HashSet<string>? Blacklist;

    public override bool Condition(ListingConditionArgs args)
    {
        var ent = args.EntityManager;
        EntityUid body;
        if (ent.TryGetComponent<MindComponent>(args.Buyer, out var mind) && mind.OwnedEntity is { } owned) body = owned;
        else body = args.Buyer;
        if (!ent.TryGetComponent<CompanyComponent>(body, out var company)) return Whitelist == null;
        if (Blacklist != null && Blacklist.Contains(company.CompanyName)) return false;
        if (Whitelist != null && !Whitelist.Contains(company.CompanyName)) return false;
        return true;
    }
}
