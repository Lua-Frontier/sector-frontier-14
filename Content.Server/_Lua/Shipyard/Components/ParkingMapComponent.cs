// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.
namespace Content.Server._Lua.Shipyard.Components;

[RegisterComponent]
public sealed partial class ParkingMapComponent : Component
{
    [DataField]
    public float NextShuttleIndex = 500f;
}
