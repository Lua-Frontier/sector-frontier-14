using Content.Server.Players;
using Content.Shared.Ghost;
using Content.Shared.Popups.GridNameDisplay;
using Robust.Server.Player;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.Server.Popups.GridNameDisplay;

/// <summary>
/// This system tracks when players move between grids and sends grid name events
/// to display when a player enters a new grid for the first time.
/// </summary>
public sealed class GridNameSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly PlayerSystem _playerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EntParentChangedMessage>(OnParentChanged);
    }

    /// <summary>
    /// Called when an entity changes parent, which includes when it moves between grids.
    /// </summary>
    private void OnParentChanged(ref EntParentChangedMessage args)
    {
        var uid = args.Entity;

        // Check if this entity is a player-controlled entity
        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        // Skip grid name display for ghosts
        if (HasComp<GhostComponent>(uid))
            return;

        // Get the new grid the player is on
        var newGridUid = args.Transform.GridUid;
        if (newGridUid == null)
            return;

        // Verify this is actually a grid
        if (!HasComp<MapGridComponent>(newGridUid))
            return;

        var player = actor.PlayerSession;

        // Get or add the component that tracks visited grids
        var visitedComp = EnsureComp<VisitedGridsComponent>(uid);

        // If player has already visited this grid, don't show the name again
        if (visitedComp.VisitedGridUids.Contains(newGridUid.Value))
            return;

        // Add the current grid to the list of visited grids
        visitedComp.VisitedGridUids.Add(newGridUid.Value);
        Dirty(uid, visitedComp);

        // Get the grid's name from metadata
        var gridName = MetaData(newGridUid.Value).EntityName;

        // Check if the name is empty or contains terms that should be displayed as "Unknown"
        if (string.IsNullOrEmpty(gridName) || gridName.ToLower().Contains("asteroid") || gridName.ToLower().Contains("wreck") || gridName.ToLower().Contains("grid"))
        {
            gridName = "Unknown";
        }

        // Send an event to the player's client to show the grid name
        RaiseNetworkEvent(new ShowGridNameEvent(gridName), player.Channel);
    }
}
