// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using Content.Lua.Shared.SpaceHazards;
using Robust.Shared.Prototypes;

namespace Content.Lua.Server.SpaceHazards;

[RegisterComponent]
public sealed partial class SectorCelestialPlacerControllerComponent : Component
{
    [DataField]
    public float SpawnDistance = 8000f;

    [DataField]
    public CelestialKind? ForceKind;

    public EntProtoId StarPrototype = "SectorCelestialStar";

    public EntProtoId BlackHolePrototype = "SectorCelestialBlackHole";

    [DataField]
    public bool Spawned;
}
