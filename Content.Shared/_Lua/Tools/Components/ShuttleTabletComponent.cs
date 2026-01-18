// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using Content.Shared.Containers.ItemSlots;

namespace Content.Shared._Lua.Tools.Components;

[RegisterComponent]
public sealed partial class ShuttleTabletComponent : Component
{
    [DataField]
    public ItemSlot IDContainerSlot = new()
    {
        Whitelist = new()
        {
            Components = ["IdCard"]
        }
    };
}
