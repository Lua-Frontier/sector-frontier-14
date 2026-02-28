// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Lua.Stargate.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class StargateConsoleComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? LinkedStargate;

    [DataField]
    public float AutoLinkRadius = 10f;
    [ViewVariables]
    public List<byte> CurrentInput = new();

    [ViewVariables]
    public byte[]? AutoDialQueue;

    [ViewVariables]
    public int AutoDialIndex;

    [ViewVariables]
    public float AutoDialAccumulator;

    [DataField]
    public SoundSpecifier DhdPressSound = new SoundCollectionSpecifier("StargateDhdPress");

    [DataField]
    public SoundSpecifier DhdDialSound = new SoundPathSpecifier("/Audio/_Lua/Effects/Stargate/pegasus_dhd_enter.ogg");
}
