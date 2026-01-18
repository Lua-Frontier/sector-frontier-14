// LuaWorld/LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld/LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Shipyard.Events;

[Serializable, NetSerializable]
public sealed class SelectDockPortMessage : BoundUserInterfaceMessage
{
    public NetEntity? SelectedDockPort { get; }
    public SelectDockPortMessage(NetEntity? selectedDockPort)
    { SelectedDockPort = selectedDockPort; }
}
[Serializable, NetSerializable]
public sealed class OpenDockSelectMessage : BoundUserInterfaceMessage
{ public OpenDockSelectMessage() { } }

