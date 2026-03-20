// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Shipyard.BUI;

[NetSerializable, Serializable]
public sealed class ParkingConsoleInterfaceState : BoundUserInterfaceState
{
    public readonly string? ShipDeedTitle;
    public readonly bool IsTargetIdPresent;
    public readonly bool IsShuttleParked;

    public ParkingConsoleInterfaceState(string? shipDeedTitle, bool isTargetIdPresent, bool isShuttleParked)
    {
        ShipDeedTitle = shipDeedTitle;
        IsTargetIdPresent = isTargetIdPresent;
        IsShuttleParked = isShuttleParked;
    }
}
