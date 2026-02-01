using Robust.Shared.Serialization;

namespace Content.Shared.Popups.GridNameDisplay;

/// <summary>
/// Network event sent from server to client to display the grid name when a player enters a grid.
/// </summary>
[Serializable, NetSerializable]
public sealed class ShowGridNameEvent : EntityEventArgs
{
    public string GridName { get; }

    public ShowGridNameEvent(string gridName)
    {
        GridName = gridName;
    }
}
