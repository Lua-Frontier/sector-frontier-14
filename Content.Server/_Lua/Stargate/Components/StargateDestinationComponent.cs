// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using System.Numerics;
using Robust.Shared.Timing;

namespace Content.Server._Lua.Stargate.Components;

[RegisterComponent]
public sealed partial class StargateDestinationComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public byte[] Address = Array.Empty<byte>();

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int Seed;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public Vector2i Origin;

    [ViewVariables]
    public EntityUid? GateUid;

    [ViewVariables]
    public bool Loaded;

    [ViewVariables]
    public bool ProgressiveLoadingActive;

    [ViewVariables]
    public TimeSpan? EmptySince;

    [ViewVariables]
    public bool Frozen;

    public HashSet<EntityUid> FrozenCollidables = new();
}
