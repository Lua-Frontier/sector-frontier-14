// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server._Lua.AmbientSpaceEffects;

[RegisterComponent]
[Access(typeof(AmbientSpaceFieldPlacerSystem))]
public sealed partial class AmbientSpaceFieldPlacerControllerComponent : Component
{
    [DataField]
    public int MinCount = 20;

    [DataField]
    public int MaxCount = 30;

    [DataField]
    public float MinDistance = 2000f;

    [DataField]
    public float MaxDistance = 30000f;

    [DataField]
    public float MinStationClearance = 1500f;

    [DataField]
    public float MinSpacingFactor = 0.85f;

    [DataField]
    public float MinCenterSeparation = 900f;

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string FieldPrototype = "AmbientSpaceFieldNebula";

    [DataField]
    public bool Spawned;
}
