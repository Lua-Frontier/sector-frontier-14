// LuaWorld/LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld/LuaCorp
// See AGPLv3.txt for details.

namespace Content.Server._Lua.SpaceWhale;

[RegisterComponent]
public sealed partial class SpaceWhaleTargetComponent : Component
{
    [DataField]
    public EntityUid Entity { get; set; }
}

