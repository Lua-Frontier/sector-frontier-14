// LuaWorld/LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld/LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Parking;

[Serializable, NetSerializable]
public enum TrafficManagerTabletUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public enum TrafficManagerShuttleStatus : byte
{
    Green,
    Orange,
    Red
}

[Serializable, NetSerializable]
public sealed class TrafficManagerTabletUiState : BoundUserInterfaceState
{
    public readonly bool Authorized;
    public readonly string? Error;
    public readonly List<TrafficManagerTabletShuttleEntry> Shuttles;

    public TrafficManagerTabletUiState(bool authorized, string? error, List<TrafficManagerTabletShuttleEntry> shuttles)
    {
        Authorized = authorized;
        Error = error;
        Shuttles = shuttles;
    }
}

[Serializable, NetSerializable]
public readonly record struct TrafficManagerTabletShuttleEntry(
    NetEntity Shuttle,
    string ShuttleName,
    string OwnerName,
    TrafficManagerShuttleStatus Status,
    int TimeRemainingSeconds,
    int ExtraMinutes,
    bool NeedsDisposal,
    bool SellEnabled);

[Serializable, NetSerializable]
public enum TrafficManagerTabletAction : byte
{
    Refresh,
    ResetTimer,
    AddTenMinutes,
    Sell
}

[Serializable, NetSerializable]
public sealed class TrafficManagerTabletUiMessage : BoundUserInterfaceMessage
{
    public readonly TrafficManagerTabletAction Action;
    public readonly NetEntity Shuttle;

    public TrafficManagerTabletUiMessage(TrafficManagerTabletAction action, NetEntity shuttle)
    {
        Action = action;
        Shuttle = shuttle;
    }
}


