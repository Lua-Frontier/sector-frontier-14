// LuaWorld/LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld/LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.GameStates;

namespace Content.Lua.Shared.Shipyard.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShipyardLuaConsoleComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public NetEntity? SelectedDockPort;

    [DataField("parkingConsole"), AutoNetworkedField]
    public bool ParkingConsole;
}
