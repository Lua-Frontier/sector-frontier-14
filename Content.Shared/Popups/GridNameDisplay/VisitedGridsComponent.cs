using Robust.Shared.GameStates;

namespace Content.Shared.Popups.GridNameDisplay;

/// <summary>
/// Tracks which grids a player has visited during a session to prevent showing
/// the grid name popup multiple times for the same grid.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VisitedGridsComponent : Component
{
    /// <summary>
    /// Set of grid EntityUids the player has already visited.
    /// </summary>
    [DataField, AutoNetworkedField]
    [ViewVariables(VVAccess.ReadOnly)]
    public HashSet<EntityUid> VisitedGridUids = new();
}
