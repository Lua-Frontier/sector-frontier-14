// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Shared._Lua.Starmap;

namespace Content.Server._Lua.Starmap.Components;

[RegisterComponent]
public sealed partial class FactionPayoutCollectorComponent : Component
{
    public const int MaxClaimHistory = 10;

    [DataField]
    public string Faction = string.Empty;

    [DataField]
    public string CurrencyPrototypePerUnit = string.Empty;

    [DataField]
    public int PayoutIntervalSeconds = 3600;

    [DataField]
    public int PayoutPerStation = 1;

    [ViewVariables]
    public int Accumulated;

    [ViewVariables]
    public TimeSpan LastAccrualAt = TimeSpan.Zero;

    /// <summary>
    /// Newest claims first. Capped at <see cref="MaxClaimHistory"/>.
    /// </summary>
    [DataField]
    public List<PayoutClaimHistoryEntry> ClaimHistory = new();
}
