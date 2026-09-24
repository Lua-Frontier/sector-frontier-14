// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

namespace Content.Lua.Shared.SpaceHazards;

[ByRefEvent]
public record struct NebulaShieldHitAttemptEvent(float Load)
{
    public bool Absorbed;
}
