// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

namespace Content.Lua.Shared.Stargate.Components;

[RegisterComponent]
public sealed partial class StargateAddressEditorConsoleComponent : Component
{
    [ViewVariables]
    public List<byte> CurrentInput = new();
}
