// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;
using Content.Shared.Shuttles.BUIStates;

namespace Content.Shared._Lua.Shuttles.UI;

[Serializable, NetSerializable]
public sealed class ShuttleTabletWindowInterfaceState(
    NavInterfaceState navState,
    DockingInterfaceState dockState,
    NetEntity? shuttle,
    int linkPower
    ) : BoundUserInterfaceState
{
    public NavInterfaceState NavState = navState;
    public DockingInterfaceState DockState = dockState;
    public NetEntity? Shuttle = shuttle;
    public int LinkPower = linkPower;
}
