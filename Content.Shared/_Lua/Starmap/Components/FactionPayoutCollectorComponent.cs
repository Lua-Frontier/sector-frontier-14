// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared.Containers.ItemSlots;
using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lua.Starmap.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class FactionPayoutCollectorComponent : Component
{
    public const int MaxClaimHistory = 10;
    public const string CashSlotId = "payout-cashSlot";

    [DataField]
    public string Faction = string.Empty;

    [DataField]
    public ProtoId<StackPrototype> CashType = "Credit";

    [DataField]
    public ItemSlot CashSlot = new();

    [DataField]
    public int PayoutIntervalSeconds = 3600;

    [DataField]
    public int PayoutPerStation = 1;

    [DataField]
    public SoundSpecifier ErrorSound =
        new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg");

    [DataField]
    public SoundSpecifier ConfirmSound =
        new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");
}
