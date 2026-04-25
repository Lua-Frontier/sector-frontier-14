// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Map;
using System.Numerics;

namespace Content.Server._Lua.Stargate.Components;

[RegisterComponent]
public sealed partial class StarGateLandingBeaconComponent : Component
{
    public const string DeedSlotId = "stargate-deed";

    [DataField]
    public float EdgeOffsetTiles = 4f;

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? BoundShuttle;

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? ReturnMapUid;

    [ViewVariables(VVAccess.ReadOnly)]
    public Vector2 ReturnWorldPosition;

    [ViewVariables(VVAccess.ReadOnly)]
    public Angle ReturnAngle;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan? RecallAt;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool RecallFtlStarted;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan? AutoReturnAt;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan NextRecallUiUpdate;

    [DataField]
    public ItemSlot DeedSlot = new();
}
