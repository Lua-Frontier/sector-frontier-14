// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.GameStates;

namespace Content.Shared._Lua.ShipCpu.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class ShipCpuComponent : Component
{
    [DataField]
    public int MaxTiles = 441;

    [DataField]
    public int MaxSide = 21;
    [DataField]
    public bool Unlimited = false;
}
