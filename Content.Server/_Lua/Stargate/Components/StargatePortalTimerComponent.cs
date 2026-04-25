// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.Timing;

namespace Content.Server._Lua.Stargate.Components;

[RegisterComponent]
public sealed partial class StargatePortalTimerComponent : Component
{
    [ViewVariables]
    public bool HasEntityPassedThrough;

    [ViewVariables]
    public TimeSpan LastEntityNearTime;

    [DataField]
    public float CloseDelay = 10f;

    [DataField]
    public float NearRadius = 3f;
}
