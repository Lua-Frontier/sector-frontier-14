// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

namespace Content.Server._Lua.Company.Components;

[RegisterComponent]
public sealed partial class FactionCaptureComponent : Component
{
    [DataField]
    public bool CaptureWholeStation;

    [DataField]
    public float CaptureRadius = 20f;

    [DataField]
    public int RequiredAttackers = 0;

    [DataField]
    public float CaptureDuration = 0f;

    [DataField]
    public bool ResetOnDefenderPresence = true;

    [DataField]
    public bool PausedIfNoAttackers = true;

    [DataField]
    public float Progress;

    [DataField]
    public string? AttackingCompany;
}
