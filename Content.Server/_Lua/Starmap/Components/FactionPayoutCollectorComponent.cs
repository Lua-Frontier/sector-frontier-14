// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

namespace Content.Server._Lua.Starmap.Components;

[RegisterComponent]
public sealed partial class FactionPayoutCollectorComponent : Component
{
    [DataField]
    public string Faction = string.Empty;

    [DataField]
    public string CurrencyPrototypePerUnit = string.Empty;

    [DataField]
    public int PayoutIntervalSeconds = 900;

    [DataField]
    public int PayoutPerSector = 15;

    [ViewVariables]
    public int Accumulated;

    [ViewVariables]
    public TimeSpan LastAccrualAt = TimeSpan.Zero;
}


