// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;

namespace Content.Shared._Lua.StationRecords;

[Serializable, NetSerializable]
public sealed class ShipCrewRosterEntry
{
    public readonly string Name;
    public readonly ShipCrewRole Role;

    public ShipCrewRosterEntry(string name, ShipCrewRole role)
    {
        Name = name;
        Role = role;
    }
}

