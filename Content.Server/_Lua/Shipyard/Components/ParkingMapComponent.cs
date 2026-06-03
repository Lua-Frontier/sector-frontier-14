// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.
namespace Content.Server._Lua.Shipyard.Components;

[RegisterComponent]
public sealed partial class ParkingMapComponent : Component
{
    [DataField]
    public int CurrentRing = 0;
    [DataField]
    public int CurrentSlotInRing = 0;
}
