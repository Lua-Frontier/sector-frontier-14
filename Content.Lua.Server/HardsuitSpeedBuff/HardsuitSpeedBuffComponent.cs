// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.
// In addition to AGPLv3, the author grants the "Мёртвый Космос" project

using Robust.Shared.Prototypes;

namespace Content.Lua.Server.HardsuitSpeedBuff;

[RegisterComponent]
public sealed partial class HardsuitSpeedBuffComponent : Component
{
    public EntProtoId Action = "ActionHardsuitSpeedBuff";

    [DataField]
    public EntityUid? ActionEntity;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float WalkModifier = 1.35f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float SprintModifier = 1.35f;

    [DataField]
    public bool Activated = false;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float PowerConsumption = 5.0f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MinPowerRequired = 20.0f;
}
