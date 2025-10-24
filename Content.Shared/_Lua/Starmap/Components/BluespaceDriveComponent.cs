// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Robust.Shared.GameStates;

namespace Content.Shared._Lua.Starmap.Components;

[RegisterComponent]
[NetworkedComponent]
public sealed partial class BluespaceDriveComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float CooldownSeconds = 600f;

    [ViewVariables]
    public TimeSpan CooldownEndsAt = TimeSpan.Zero;
}


