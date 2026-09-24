// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Prototypes;

namespace Content.Lua.Server.SpaceHazards;

[RegisterComponent]
public sealed partial class SectorBackgroundPlanetPlacerControllerComponent : Component
{
    [DataField]
    public int MinCount = 2;

    [DataField]
    public int MaxCount = 4;

    [DataField]
    public float MinDistance = 6000f;

    [DataField]
    public float MaxDistance = 12000f;

    [DataField]
    public float MinStationClearance = 2000f;

    [DataField]
    public EntProtoId PlanetPrototype = "SectorBackgroundPlanet";

    [DataField]
    public bool Spawned;
}
