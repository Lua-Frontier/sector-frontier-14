// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.
using Content.Lua.Shared.Stargate;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
namespace Content.Lua.Shared.Stargate.Components;
[RegisterComponent, NetworkedComponent]
public sealed partial class StargateMinimapDiskComponent : Component
{
    [ViewVariables] public Dictionary<string, StargateMinimapPlanetData> Planets = new();
    [ViewVariables] public byte[] CurrentPlanetAddress = Array.Empty<byte>();
}
