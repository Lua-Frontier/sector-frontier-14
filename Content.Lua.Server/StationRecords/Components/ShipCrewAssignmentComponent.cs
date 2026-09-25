// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Lua.Shared.StationRecords;
using Robust.Shared.Network;

namespace Content.Lua.Server.StationRecords.Components;

[RegisterComponent]
public sealed partial class ShipCrewAssignmentComponent : Component
{
    [DataField]
    public EntityUid? ShuttleUid;

    [DataField]
    public string ShipName = string.Empty;

    [DataField]
    public ShipCrewRole Role;

    [DataField]
    public NetUserId? AssignedUserId;
}

