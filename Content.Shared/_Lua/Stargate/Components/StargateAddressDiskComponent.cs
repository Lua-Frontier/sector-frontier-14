// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.GameStates;

namespace Content.Shared._Lua.Stargate.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StargateAddressDiskComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<List<byte>> Addresses = new();
}
