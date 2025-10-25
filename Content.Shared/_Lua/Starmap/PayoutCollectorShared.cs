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
public sealed class PayoutCollectorBuiState : BoundUserInterfaceState
{
    public int OwnedSectors;
    public int Accumulated;
    public string Faction;

    public PayoutCollectorBuiState(int ownedSectors, int accumulated, string faction)
    {
        OwnedSectors = ownedSectors;
        Accumulated = accumulated;
        Faction = faction;
    }
}

[Serializable, NetSerializable]
public sealed class PayoutCollectorClaimMessage : BoundUserInterfaceMessage
{ }


