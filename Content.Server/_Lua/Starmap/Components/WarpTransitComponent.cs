// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Robust.Shared.Map;
using System.Numerics;

namespace Content.Server._Lua.Starmap.Components;

[RegisterComponent]
public sealed partial class WarpTransitComponent : Component
{
    [DataField]
    public MapId TargetMap;

    [DataField]
    public Vector2 TargetPosition;
}


