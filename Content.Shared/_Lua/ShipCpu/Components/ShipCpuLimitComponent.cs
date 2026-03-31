// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.GameStates;

namespace Content.Shared._Lua.ShipCpu.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShipCpuLimitComponent : Component
{
    [DataField, AutoNetworkedField]
    public int MaxTiles = 81;

    [DataField, AutoNetworkedField]
    public int MaxSide = 9;

    [DataField, AutoNetworkedField]
    public bool Unlimited = false;
}
