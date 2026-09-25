using Content.Lua.Shared.Shuttles;
// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

namespace Content.Lua.Shared.Shuttles.Components;

[RegisterComponent]
public sealed partial class MagneticGrabberCooldownComponent : Component
{
    [ViewVariables]
    public TimeSpan NextLatchAllowed = TimeSpan.Zero;
}

