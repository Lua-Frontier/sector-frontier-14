// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

namespace Content.Lua.Server.ExplodeOnInit;

[RegisterComponent]
public sealed partial class ExplodeOnInitComponent : Component
{
    [DataField("explodeOnInit")] public bool ExplodeOnInit = true;

    [DataField("timeUntilDetonation")] public float TimeUntilDetonation = 1f;
}

