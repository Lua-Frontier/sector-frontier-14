// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

namespace Content.Server._Lua.SpaceWhale;

[RegisterComponent]
public sealed partial class SpaceWhaleExposureComponent : Component
{
    [DataField]
    public TimeSpan EnteredAt;
}
