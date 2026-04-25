// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Lua.StationTeleporter.Components;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Lua.StationTeleporter;

[Serializable, NetSerializable]
public enum StationTeleporterUIKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class StationTeleporterState : BoundUserInterfaceState
{
    public NetEntity SelfUid;
    public TeleporterType TeleporterType;
    public List<StationTeleporterStatus> Teleporters;

    public StationTeleporterState(NetEntity selfUid, TeleporterType teleporterType, List<StationTeleporterStatus> teleporters)
    {
        SelfUid = selfUid;
        TeleporterType = teleporterType;
        Teleporters = teleporters;
    }
}

[Serializable, NetSerializable]
public sealed class StationTeleporterStatus
{
    public NetEntity Uid;
    public NetCoordinates Coordinates;
    public NetCoordinates? LinkedCoordinates;
    public string Name;
    public bool Powered;

    public StationTeleporterStatus(NetEntity uid, NetCoordinates coordinates, NetCoordinates? linkedCoordinates, string name, bool powered)
    {
        Uid = uid;
        Coordinates = coordinates;
        LinkedCoordinates = linkedCoordinates;
        Name = name;
        Powered = powered;
    }
}

[Serializable, NetSerializable]
public sealed class StationTeleporterClickMessage : BoundUserInterfaceMessage
{
    public NetEntity TargetUid;

    public StationTeleporterClickMessage(NetEntity targetUid)
    {
        TargetUid = targetUid;
    }
}

[Serializable, NetSerializable]
public sealed class StationTeleporterRenameMessage : BoundUserInterfaceMessage
{
    public string NewName;

    public StationTeleporterRenameMessage(string newName)
    {
        NewName = newName;
    }
}

[Serializable, NetSerializable]
public enum TeleporterPortalVisuals : byte
{
    Color
}
