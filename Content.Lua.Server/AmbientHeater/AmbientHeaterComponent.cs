// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

namespace Content.Lua.Server.AmbientHeater;

[RegisterComponent]
public sealed partial class AmbientHeaterComponent : Component
{
    [DataField("targetTemperature")]
    [ViewVariables(VVAccess.ReadWrite)]
    public float TargetTemperature = 293.15f;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("heatPerSecond")]
    public float HeatPerSecond = 100f;

    [ViewVariables(VVAccess.ReadOnly)] [DataField("requiresPower")]
    public bool RequiresPower;

    [ViewVariables(VVAccess.ReadOnly)] public bool Powered = false;
}
