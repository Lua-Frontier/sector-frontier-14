// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Lua.Stargate.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class StargateComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public byte PointOfOrigin;

    [DataField]
    public SoundSpecifier OpenSound = new SoundPathSpecifier("/Audio/_Lua/Effects/Stargate/pegasus_wormhole_open.ogg");

    [DataField]
    public SoundSpecifier CloseSound = new SoundPathSpecifier("/Audio/_Lua/Effects/Stargate/wormhole_close.ogg");

    [DataField]
    public SoundSpecifier ChevronSound = new SoundCollectionSpecifier("StargateChevronEngage");

    [DataField]
    public SoundSpecifier DialFailSound = new SoundCollectionSpecifier("StargateDialFail");

    [DataField]
    public SoundSpecifier IdleSound = new SoundPathSpecifier("/Audio/_Lua/Effects/Stargate/wormhole_idle.ogg");

    [ViewVariables]
    public EntityUid? IdleSoundEntity;

    [DataField]
    public byte AddressLength = 6;

    [DataField]
    public byte SymbolCount = 40;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string? AddressPreset;

    [ViewVariables(VVAccess.ReadOnly)]
    public byte[]? Address;

    [DataField]
    public float ChevronDelay = 0.6f;

    [DataField]
    public float KawooshDelay = 2.0f;
}
