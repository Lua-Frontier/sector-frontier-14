// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.GameStates;

namespace Content.Shared._Lua.StationRecords.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShipCrewAssignmentStatusComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? ShuttleUid;

    [DataField, AutoNetworkedField]
    public ShipCrewRole Role;
}

