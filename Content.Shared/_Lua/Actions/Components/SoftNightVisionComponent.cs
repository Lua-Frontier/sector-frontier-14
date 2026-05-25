// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lua.Actions.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class SoftNightVisionComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public bool Enable = false;

    public override bool SendOnlyToOwner => true;

    [DataField]
    public float TimeForFinalEnergy = 5f; // Seconds

    [DataField]
    public float InnerRadius = 2.7f;

    [DataField]
    public float InnerEnergy = 0.23f;

    [DataField]
    public float InnerSoftness = 1f;

    [DataField]
    public float OuterRadius = 4f;

    [DataField]
    public float OuterEnergy = 0.08f;

    [DataField]
    public float OuterSoftness = 2f;

    [DataField]
    public Color Color = Color.FromHex("#cfd6d9");

    [ViewVariables]
    public EntityUid? InnerLight;

    [ViewVariables]
    public EntityUid? OuterLight;

    [DataField]
    public EntProtoId Action = "ActionToggleSoftNightVision";

    [ViewVariables]
    public EntityUid? ActionEntity;
}
