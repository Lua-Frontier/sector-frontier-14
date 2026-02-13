// LuaWorld/LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld/LuaCorp
// See AGPLv3.txt for details.
using Robust.Shared.Prototypes;

namespace Content.Server._Lua.Botany;

[RegisterComponent]
public sealed partial class SoilFlatpackComponent : Component
{
    [DataField(required: true)]
    public EntProtoId FlatpackPrototype;
}
