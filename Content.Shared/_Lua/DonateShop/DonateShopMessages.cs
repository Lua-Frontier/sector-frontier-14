// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;
using Content.Shared.FixedPoint;
using Content.Shared.Store;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lua.DonateShop;

[Serializable, NetSerializable]
public sealed class RequestDonateShopStateMessage : EntityEventArgs;
[Serializable, NetSerializable]
public sealed class RequestDonateShopOpenMessage : EntityEventArgs;
[Serializable, NetSerializable]
public sealed class RequestDonateShopBuyMessage(string listingId) : EntityEventArgs
{ public string ListingId = listingId; }

[Serializable, NetSerializable]
public sealed class DonateShopStateMessage(bool canAccess, bool hasSubscription, string tierName, string subscriptionStatus, HashSet<ListingDataWithCostModifiers>? listings = null, Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2>? balance = null, int bankBalance = 0, bool hasBankBalance = false, string? errorLocKey = null, List<string>? activeTierNames = null, long lunaCoinBalance = 0) : EntityEventArgs
{
    public bool CanAccess = canAccess;
    public bool HasSubscription = hasSubscription;
    public string TierName = tierName;
    public string SubscriptionStatus = subscriptionStatus;
    public HashSet<ListingDataWithCostModifiers> Listings = listings ?? [];
    public Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2> Balance = balance ?? [];
    public int BankBalance = bankBalance;
    public bool HasBankBalance = hasBankBalance;
    public string? ErrorLocKey = errorLocKey;
    public List<string> ActiveTierNames = activeTierNames ?? [];
    public long LunaCoinBalance = lunaCoinBalance;
}
