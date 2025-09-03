using Robust.Shared.GameStates;

namespace Content.Shared.Carrying;

/// <summary>
/// Marker component: entities with this cannot carry other entities using the Carrying system.
/// Placed in Shared so it can be referenced from prototypes on both client and server.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CantCarryOthersComponent : Component
{
}


