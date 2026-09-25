// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

namespace Content.Lua.Shared.Starmap.Components;

[RegisterComponent]
public sealed partial class StarMapCoordinatesDiskComponent : Component
{
    [DataField]
    public List<string> AllowedSectors = new();

    [DataField]
    public List<string> AllowedSectorIds = new();

    [DataField]
    public bool AllowFtlToCentCom = false;
}


