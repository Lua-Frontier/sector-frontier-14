// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Lua.StationRecords;

namespace Content.Server._Lua.StationRecords.Components;

[RegisterComponent]
public sealed partial class ShipCrewAssignmentComponent : Component
{
    [DataField]
    public EntityUid? ShuttleUid;

    [DataField]
    public string ShipName = string.Empty;

    [DataField]
    public ShipCrewRole Role;
}

