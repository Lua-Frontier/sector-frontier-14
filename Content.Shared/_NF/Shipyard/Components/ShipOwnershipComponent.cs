using Robust.Shared.GameStates;
using Robust.Shared.Network;

namespace Content.Shared._NF.Shipyard.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShipOwnershipComponent : Component
{
    [DataField, AutoNetworkedField]
    public NetUserId OwnerUserId;
}
