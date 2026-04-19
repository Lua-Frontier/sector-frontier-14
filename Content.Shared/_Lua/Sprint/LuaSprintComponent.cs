// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Lua.Sprint;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LuaSprintComponent : Component
{
    [DataField, AutoNetworkedField]
    public float MaxSprint = 100f;

    [DataField, AutoNetworkedField]
    public float CurrentSprint = 100f;

    [DataField]
    public float DrainPerSecond = 6f;

    [DataField]
    public float RegenPerSecond = 7f;

    [DataField]
    public float RegenDelay = 0.3f;

    [DataField]
    public float RecoverThresholdFraction = 0.3f;

    [DataField, AutoNetworkedField]
    public bool Depleted;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan LastSprintTime = TimeSpan.Zero;
}