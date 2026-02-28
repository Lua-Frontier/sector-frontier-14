// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

namespace Content.Server._Lua.Stargate.Components;

[RegisterComponent]
public sealed partial class StargateIrisAnimatingComponent : Component
{
    [ViewVariables]
    public float Accumulator;

    [ViewVariables]
    public float Duration = 1.344f;

    [ViewVariables]
    public bool IsOpening;
}
