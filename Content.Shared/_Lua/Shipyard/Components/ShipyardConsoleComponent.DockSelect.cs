// LuaWorld/LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld/LuaCorp
// See AGPLv3.txt for details.

namespace Content.Shared._NF.Shipyard.Components;

public sealed partial class ShipyardConsoleComponent
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public NetEntity? SelectedDockPort;
}

