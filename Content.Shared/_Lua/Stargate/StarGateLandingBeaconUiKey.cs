// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Stargate;

[Serializable, NetSerializable]
public enum StarGateLandingBeaconUiKey : byte
{ Key }

[Serializable, NetSerializable]
public sealed class StarGateLandingBeaconBoundUserInterfaceState : BoundUserInterfaceState
{
    public string? ShuttleName;

    public bool IsBound;

    public MapCoordinates BeaconPosition;

    public NetEntity? ShuttleNetEntity;

    public bool CanRecall;

    public bool RecallPending;

    public TimeSpan RecallRemaining;

    public StarGateLandingBeaconBoundUserInterfaceState(string? shuttleName, bool isBound, MapCoordinates beaconPosition, NetEntity? shuttleNetEntity, bool canRecall, bool recallPending, TimeSpan recallRemaining)
    {
        ShuttleName = shuttleName;
        IsBound = isBound;
        BeaconPosition = beaconPosition;
        ShuttleNetEntity = shuttleNetEntity;
        CanRecall = canRecall;
        RecallPending = recallPending;
        RecallRemaining = recallRemaining;
    }
}

[Serializable, NetSerializable]
public sealed class StarGateLandingBeaconSummonMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class StarGateLandingBeaconFlyToMessage : BoundUserInterfaceMessage
{
    public MapCoordinates Coordinates;
    public Angle Angle;

    public StarGateLandingBeaconFlyToMessage(MapCoordinates coordinates, Angle angle)
    {
        Coordinates = coordinates;
        Angle = angle;
    }
}

[Serializable, NetSerializable]
public sealed class StarGateLandingBeaconRecallMessage : BoundUserInterfaceMessage
{
}
