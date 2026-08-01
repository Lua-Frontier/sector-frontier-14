using Robust.Shared.Prototypes; // Lua
using Robust.Shared.Serialization;
using Content.Shared._NF.Shipyard.Prototypes; // Lua

namespace Content.Shared._NF.Shipyard.BUI;

[NetSerializable, Serializable]
public sealed class ShipyardConsoleInterfaceState : BoundUserInterfaceState
{
    public int Balance;
    public readonly bool AccessGranted;
    public readonly string? ShipDeedTitle;
    public int ShipSellValue;
    public readonly bool IsTargetIdPresent;
    public readonly byte UiKey;

    public readonly (List<string> available, List<string> unavailable) ShipyardPrototypes;
    public readonly string ShipyardName;
    public readonly bool FreeListings;
    public readonly float SellRate;
    public readonly Dictionary<ProtoId<VesselPrototype>, int> LimitedCounts; // Lua

    public ShipyardConsoleInterfaceState(
        int balance,
        bool accessGranted,
        string? shipDeedTitle,
        int shipSellValue,
        bool isTargetIdPresent,
        byte uiKey,
        (List<string> available, List<string> unavailable) shipyardPrototypes,
        string shipyardName,
        bool freeListings,
        float sellRate,
        Dictionary<ProtoId<VesselPrototype>, int> limitedCounts) // Lua: Added limitedCounts
    {
        Balance = balance;
        AccessGranted = accessGranted;
        ShipDeedTitle = shipDeedTitle;
        ShipSellValue = shipSellValue;
        IsTargetIdPresent = isTargetIdPresent;
        UiKey = uiKey;
        ShipyardPrototypes = shipyardPrototypes;
        ShipyardName = shipyardName;
        FreeListings = freeListings;
        SellRate = sellRate;
        LimitedCounts = limitedCounts; // Lua
    }
}
