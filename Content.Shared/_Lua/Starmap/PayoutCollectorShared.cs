// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Starmap;

[NetSerializable, Serializable]
public enum PayoutCollectorUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class PayoutClaimHistoryEntry
{
    public string CharacterName;
    public int Amount;
    public bool IsDeposit;

    public PayoutClaimHistoryEntry(string characterName, int amount, bool isDeposit = false)
    {
        CharacterName = characterName;
        Amount = amount;
        IsDeposit = isDeposit;
    }
}

[Serializable, NetSerializable]
public sealed class PayoutCollectorBuiState : BoundUserInterfaceState
{
    public int OwnedStations;
    public int Accumulated;
    public int Deposit;
    public string Faction;
    public int PayoutPerStation;
    public int IntervalSeconds;
    public TimeSpan NextPayoutAt;
    public List<PayoutClaimHistoryEntry> ClaimHistory;

    public PayoutCollectorBuiState(
        int ownedStations,
        int accumulated,
        int deposit,
        string faction,
        int payoutPerStation,
        int intervalSeconds,
        TimeSpan nextPayoutAt,
        List<PayoutClaimHistoryEntry> claimHistory)
    {
        OwnedStations = ownedStations;
        Accumulated = accumulated;
        Deposit = deposit;
        Faction = faction;
        PayoutPerStation = payoutPerStation;
        IntervalSeconds = intervalSeconds;
        NextPayoutAt = nextPayoutAt;
        ClaimHistory = claimHistory;
    }
}

[Serializable, NetSerializable]
public sealed class PayoutCollectorWithdrawMessage : BoundUserInterfaceMessage
{
    public int Amount;

    public PayoutCollectorWithdrawMessage(int amount)
    {
        Amount = amount;
    }
}

[Serializable, NetSerializable]
public sealed class PayoutCollectorDepositMessage : BoundUserInterfaceMessage
{
}
