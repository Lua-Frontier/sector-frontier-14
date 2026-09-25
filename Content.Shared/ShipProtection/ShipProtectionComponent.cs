using Robust.Shared.GameStates;

namespace Content.Shared.ShipProtection;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShipProtectionComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan ProtectionExpiresAt;
}

