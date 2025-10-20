using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;


namespace Content.Shared._Lua.ThermalCloack;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ThermalCloakComponent : Component
{
    [DataField]
    public bool Enabled = false;

    [DataField]
    public EntProtoId ToggleAction = "ActionToggleThermalCloak";

    [AutoNetworkedField]
    public EntityUid? ToggleActionEntity;

    [AutoNetworkedField]
    public EntityUid? Wearer;

    [DataField]
    public float TargetCelsius = -15f;

    [DataField]
    public float CoolingRateKps = 15f;
}

public sealed partial class ToggleThermalCloakEvent : InstantActionEvent
{
}


