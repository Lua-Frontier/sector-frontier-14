// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.Timing;

namespace Content.Server._Lua.Shuttles.Components;

[RegisterComponent]
public sealed partial class ShuttleFreezeStateComponent : Component
{
    [ViewVariables]
    public TimeSpan? EmptySince;
    [ViewVariables]
    public bool Frozen;
}
