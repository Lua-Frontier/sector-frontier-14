// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.
namespace Content.Shared._Lua.AiShuttle;

[RegisterComponent]
public sealed partial class AiShuttleKillSwitchComponent : Component
{
    [DataField]
    public float SelfDestructTimeoutSeconds = 300f;

    [DataField]
    public float ConsoleCheckIntervalSeconds = 60f;

    [DataField]
    public float ExplosionIntensity = 15000f;

    [DataField]
    public float ExplosionSlope = 5f;

    [DataField]
    public float MaxExplosionIntensity = 20f;

    [ViewVariables, DataField(serverOnly: true)]
    public bool IsActive = false;

    [ViewVariables, DataField(serverOnly: true)]
    public float TimeRemaining = 0f;

    [ViewVariables, DataField(serverOnly: true)]
    public float LastConsoleFoundTime = 0f;

    [ViewVariables, DataField(serverOnly: true)]
    public float LastConsoleCheckTime = 0f;
}
