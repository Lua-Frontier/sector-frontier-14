// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.
namespace Content.Server._Lua.Shuttles.Components;

[RegisterComponent]
public sealed partial class MagneticLatchTargetComponent : Component
{
    [DataField]
    public List<EntityUid> Magnets = new();
}

