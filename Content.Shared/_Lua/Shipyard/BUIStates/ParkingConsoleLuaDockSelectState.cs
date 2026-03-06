// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Lua.Shipyard.BUI;
using Content.Shared.Shuttles.BUIStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Shipyard.BUIStates;

[NetSerializable, Serializable]
public sealed class ParkingConsoleLuaDockSelectState : BoundUserInterfaceState
{
    public ParkingConsoleInterfaceState BaseState { get; }
    public NavInterfaceState DockNavState { get; }
    public NetEntity? SelectedDockPort { get; }

    public ParkingConsoleLuaDockSelectState(ParkingConsoleInterfaceState baseState, NavInterfaceState dockNavState, NetEntity? selectedDockPort)
    {
        BaseState = baseState;
        DockNavState = dockNavState;
        SelectedDockPort = selectedDockPort;
    }
}
