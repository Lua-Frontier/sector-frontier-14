// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server._Lua.HardsuitReanimation;

[RegisterComponent]
public sealed partial class HardsuitReanimationComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool Activated = true;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool IsReanimating = false;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan CooldownDuration = TimeSpan.FromMinutes(1);

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan DeathDetectionDelay = TimeSpan.FromSeconds(5);

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan StepDelay = TimeSpan.FromSeconds(4);

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int IchorAmount = 15;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int OmnizineAmount = 15;

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string EmpPulseEffect = "EffectEmpPulse";

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan LastReanimationTime = TimeSpan.Zero;
}
