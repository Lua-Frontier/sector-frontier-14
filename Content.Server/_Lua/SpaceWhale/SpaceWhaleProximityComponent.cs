// LuaWorld/LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld/LuaCorp
// See AGPLv3.txt for details.

namespace Content.Server._Lua.SpaceWhale;

[RegisterComponent]
public sealed partial class SpaceWhaleProximityComponent : Component
{
    [DataField]
    public bool WasOutsideSafeZone;

    [DataField]
    public EntityUid? AmbientStream;

    [DataField]
    public TimeSpan NextAppearCue;
}

