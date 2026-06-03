// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;

namespace Content.Shared._Lua.MedicalScanner.UI;

[Serializable, NetSerializable]
public enum HealthAnalyzerRotTime : byte
{
    None,
    TenMinutes,
    TwentyMinutes,
    ThirtyMinutes,
    FortyFiveMinutes,
    Hour,
    TwoHours,
    ThreeHours,
    FourHours,
    SixHours,
    EightHours,
    TenHours,
    TwelveHours,
    FifteenHours,
    EighteenHours,
    Day
}
