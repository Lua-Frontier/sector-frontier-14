// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared.Mind;
using Content.Shared.Store;
using Robust.Server.Player;
using Robust.Shared.IoC;

namespace Content.Server.Store.Conditions;

public sealed partial class BuyerSponsorOwnerCondition : ListingCondition
{
    [DataField("ownerLogin", required: true)]
    public string OwnerLogin = string.Empty;

    public override bool Condition(ListingConditionArgs args)
    {
        if (!args.EntityManager.TryGetComponent<MindComponent>(args.Buyer, out var mind)) return false;
        if (mind.UserId is not { } userId) return false;
        var playerManager = IoCManager.Resolve<IPlayerManager>();
        if (!playerManager.TryGetSessionById(userId, out var session)) return false;
        return string.Equals(session.Name, OwnerLogin, StringComparison.OrdinalIgnoreCase);
    }
}
