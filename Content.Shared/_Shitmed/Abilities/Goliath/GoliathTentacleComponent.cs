using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Shitmed.GoliathTentacle;

/// <summary>
/// Component that grants the entity the ability to use goliath tentacles.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GoliathTentacleComponent : Component
{
    public EntProtoId? Action = "ActionGoliathTentacleCrew";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;
}
