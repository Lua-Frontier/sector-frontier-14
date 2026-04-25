// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

namespace Content.Server._Lua.Stargate.Components;

[RegisterComponent]
public sealed partial class StargateDialingComponent : Component
{
    [ViewVariables]
    public byte[] Symbols = Array.Empty<byte>();

    [ViewVariables]
    public EntityUid DestGateUid;

    [ViewVariables]
    public EntityUid DestMapUid;

    [ViewVariables]
    public int ChevronIndex;

    [ViewVariables]
    public float Accumulator;

    [ViewVariables]
    public float ChevronDelay = 0.6f;

    [ViewVariables]
    public float KawooshDelay = 2.0f;

    [ViewVariables]
    public bool InKawoosh;

    [ViewVariables]
    public EntityUid ConsoleUid;
}
