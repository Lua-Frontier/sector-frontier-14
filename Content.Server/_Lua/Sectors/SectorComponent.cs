// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

namespace Content.Server._Lua.Sectors; // Fallback

[RegisterComponent]
public sealed partial class SectorComponent : Component
{
    [DataField]
    public bool Enabled = true;

    [DataField]
    public string Config = string.Empty;

    [DataField]
    public List<string>? Configs;
}


