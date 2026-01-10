// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;

namespace Content.Shared._Lua.StationRecords;

public static class ShipCrewManagement
{
    public const string CaptainIdSlotId = "GeneralStationRecordConsole-captainId";
    public const string TargetIdSlotId = "GeneralStationRecordConsole-targetId";

    public static string GetRoleLocKey(ShipCrewRole role)
    {
        return role switch
        {
            ShipCrewRole.Janitor => "ship-crew-role-janitor",
            ShipCrewRole.Engineer => "ship-crew-role-engineer",
            ShipCrewRole.Supply => "ship-crew-role-supply",
            ShipCrewRole.Security => "ship-crew-role-security",
            ShipCrewRole.Scientist => "ship-crew-role-scientist",
            ShipCrewRole.Paramedic => "ship-crew-role-paramedic",
            ShipCrewRole.Cook => "ship-crew-role-cook",
            ShipCrewRole.Gunner => "ship-crew-role-gunner",
            ShipCrewRole.Pilot => "ship-crew-role-pilot",
            _ => "ship-crew-role-unknown"
        };
    }
}

[Serializable, NetSerializable]
public enum ShipCrewRole : byte
{
    Janitor,
    Engineer,
    Supply,
    Security,
    Scientist,
    Paramedic,
    Cook,
    Gunner,
    Pilot
}

[Serializable, NetSerializable]
public sealed class AssignShipCrewRoleMsg : BoundUserInterfaceMessage
{
    public ShipCrewRole Role { get; }

    public AssignShipCrewRoleMsg(ShipCrewRole role)
    {
        Role = role;
    }
}

[Serializable, NetSerializable]
public sealed class FireShipCrewByRecordMsg : BoundUserInterfaceMessage
{
    public uint RecordId { get; }

    public FireShipCrewByRecordMsg(uint recordId)
    {
        RecordId = recordId;
    }
}

[Serializable, NetSerializable]
public sealed class SetShipCrewRoleByNameMsg : BoundUserInterfaceMessage
{
    public string Name { get; }
    public ShipCrewRole Role { get; }

    public SetShipCrewRoleByNameMsg(string name, ShipCrewRole role)
    {
        Name = name;
        Role = role;
    }
}

[Serializable, NetSerializable]
public sealed class FireShipCrewByNameMsg : BoundUserInterfaceMessage
{
    public string Name { get; }

    public FireShipCrewByNameMsg(string name)
    {
        Name = name;
    }
}

