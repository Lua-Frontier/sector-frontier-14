// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using Content.Shared._Lua.SpaceHazards;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server._Lua.SpaceHazards;

[RegisterComponent]
[Access(typeof(SectorCelestialPlacerSystem))]
public sealed partial class SectorCelestialPlacerControllerComponent : Component
{
    [DataField]
    public float SpawnDistance = 8000f;

    [DataField]
    public CelestialKind? ForceKind;

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string StarPrototype = "SectorCelestialStar";

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string BlackHolePrototype = "SectorCelestialBlackHole";

    [DataField]
    public bool Spawned;
}
