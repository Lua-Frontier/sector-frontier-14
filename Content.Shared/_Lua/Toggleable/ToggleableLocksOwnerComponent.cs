// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Robust.Shared.GameStates;

namespace Content.Shared._Lua.Toggleable;

[RegisterComponent, NetworkedComponent]
public sealed partial class ToggleableLocksOwnerComponent : Component
{
    [DataField(required: true)]
    public HashSet<string> ClothingPrototypes = new();

    [DataField]
    public int ActiveLocks;
}


