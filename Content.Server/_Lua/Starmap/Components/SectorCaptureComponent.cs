// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

namespace Content.Server._Lua.Starmap.Components;

[RegisterComponent]
public sealed partial class SectorCaptureComponent : Component
{
    [DataField]
    public float CaptureDurationSeconds = 300f;

    [DataField]
    public string Faction = string.Empty;

    [DataField]
    public string ColorHex = "#FFFFFF";

    [ViewVariables]
    public bool IsCapturing = false;

    [ViewVariables]
    public TimeSpan CaptureStartedAt = TimeSpan.Zero;
}


