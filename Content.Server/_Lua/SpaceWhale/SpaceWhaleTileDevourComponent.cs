// LuaWorld/LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld/LuaCorp
// See AGPLv3.txt for details.

using Content.Shared.Damage;
using Content.Shared.Whitelist;

namespace Content.Server._Lua.SpaceWhale;

[RegisterComponent]
public sealed partial class SpaceWhaleTileDevourComponent : Component
{
    [DataField]
    public float DevourInterval = 0.5f;

    [DataField]
    public int TilesPerBite = 3;

    [DataField]
    public int EntitiesPerBite = 6;

    [DataField]
    public DamageSpecifier Damage = new();

    [DataField]
    public EntityWhitelist? IgnoreWhitelist;

    [ViewVariables]
    public float Accumulator;

    [ViewVariables]
    public TimeSpan NextDevourPopup;
}

