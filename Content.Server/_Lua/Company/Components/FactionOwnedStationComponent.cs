// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

namespace Content.Server._Lua.Company.Components;

[RegisterComponent]
public sealed partial class FactionOwnedStationComponent : Component
{
    [DataField]
    public string? OriginalCompany;

    [DataField]
    public string? OriginalStationName;

    [DataField]
    public string? CurrentCompany;

    [DataField]
    public bool CanBeCaptured = true;

    [DataField]
    public bool DisableLatejoinWhenLost = true;

    [DataField]
    public bool DisableJobsWhenLost = true;

    [DataField]
    public bool MainBase;
}