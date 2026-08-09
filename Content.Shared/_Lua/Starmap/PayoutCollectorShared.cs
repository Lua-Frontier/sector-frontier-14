// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
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

    public PayoutClaimHistoryEntry(string characterName, int amount)
    {
        CharacterName = characterName;
        Amount = amount;
    }
}

[Serializable, NetSerializable]
public sealed class PayoutCollectorBuiState : BoundUserInterfaceState
{
    public int OwnedStations;
    public int Accumulated;
    public string Faction;
    public int PayoutPerStation;
    public int IntervalSeconds;
    public List<PayoutClaimHistoryEntry> ClaimHistory;

    public PayoutCollectorBuiState(
        int ownedStations,
        int accumulated,
        string faction,
        int payoutPerStation,
        int intervalSeconds,
        List<PayoutClaimHistoryEntry> claimHistory)
    {
        OwnedStations = ownedStations;
        Accumulated = accumulated;
        Faction = faction;
        PayoutPerStation = payoutPerStation;
        IntervalSeconds = intervalSeconds;
        ClaimHistory = claimHistory;
    }
}

[Serializable, NetSerializable]
public sealed class PayoutCollectorClaimMessage : BoundUserInterfaceMessage
{
}
