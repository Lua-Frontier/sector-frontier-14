// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

namespace Content.Lua.Server.TriggerOnEnterGrid;

[RegisterComponent]
public sealed partial class TriggerOnEnterGridComponent : Component
{
    public bool ReadyToTrigger = false;
}
