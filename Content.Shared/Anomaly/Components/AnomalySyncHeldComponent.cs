using Robust.Shared.GameStates;

namespace Content.Shared.Anomaly.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AnomalySyncHeldComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Compressing;
}
