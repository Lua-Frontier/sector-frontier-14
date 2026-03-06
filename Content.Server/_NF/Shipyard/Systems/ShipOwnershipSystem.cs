using Content.Server.GameTicking;
using Content.Server._Lua.Shipyard.Systems;
using Content.Server._Lua.StationRecords.Components;
using Content.Server.Mind;
using Content.Server.StationEvents.Events;
using Content.Shared._NF.Shipyard;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared.Access.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._NF.Shipyard.Systems;

/// <summary>
/// Manages ship ownership and handles cleanup of ships when owners are offline too long
/// </summary>
public sealed class ShipOwnershipSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly LinkedLifecycleGridSystem _linkedLifecycleGrid = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly ShuttleParkingSystem _parking = default!;

    private readonly HashSet<EntityUid> _pendingDeletionShips = new();

    // Timer for deletion checks
    private TimeSpan _nextDeletionCheckTime;
    private const int DeletionCheckIntervalSeconds = 60;

    public override void Initialize()
    {
        base.Initialize();

        // Subscribe to player events to track when they join/leave
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;

        // Initialize tracking for ships
        SubscribeLocalEvent<ShipOwnershipComponent, ComponentStartup>(OnShipOwnershipStartup);
        SubscribeLocalEvent<ShipOwnershipComponent, ComponentShutdown>(OnShipOwnershipShutdown);

        // Initialize the deletion check timer
        _nextDeletionCheckTime = _gameTiming.CurTime;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    /// <summary>
    /// Register a ship as being owned by a player
    /// </summary>
    public void RegisterShipOwnership(EntityUid gridUid, ICommonSession owningPlayer)
    {
        // Don't register ownership if the entity isn't valid
        if (!EntityManager.EntityExists(gridUid))
            return;

        // Add ownership component to the ship
        var comp = EnsureComp<ShipOwnershipComponent>(gridUid);
        comp.OwnerUserId = owningPlayer.UserId;
        comp.IsOwnerOnline = true;
        comp.LastStatusChangeTime = _gameTiming.CurTime;

        Dirty(gridUid, comp);

        // Log ship registration
        Logger.InfoS("shipOwnership", $"Registered ship {ToPrettyString(gridUid)} to player {owningPlayer.Name} ({owningPlayer.UserId})");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Only check for ship deletion every DeletionCheckIntervalSeconds
        if (_gameTiming.CurTime < _nextDeletionCheckTime)
            return;

        // Update next check time
        _nextDeletionCheckTime = _gameTiming.CurTime + TimeSpan.FromSeconds(DeletionCheckIntervalSeconds);

        // Log that we're checking for ships to delete
        Logger.DebugS("shipOwnership", $"Checking for abandoned ships to delete");

        var onlineCrewNames = GetOnlineCrewNames();

        // Check for ships that need to be deleted due to owner absence
        var query = EntityQueryEnumerator<ShipOwnershipComponent>();
        while (query.MoveNext(out var uid, out var ownership))
        {
            // Skip ships with online owners
            if (ownership.IsOwnerOnline)
                continue;

            // Calculate how long the owner has been offline
            var offlineTime = _gameTiming.CurTime - ownership.LastStatusChangeTime;
            var timeoutSeconds = TimeSpan.FromSeconds(ownership.DeletionTimeoutSeconds);

            // Check if we've passed the timeout
            if (offlineTime >= timeoutSeconds)
            {
                if (_parking.IsParked(uid))
                {
                    Logger.DebugS("shipOwnership", $"Skipping deletion of parked shuttle {ToPrettyString(uid)}");
                    continue;
                }

                if (HasOnlineAssignedCrew(uid, onlineCrewNames))
                {
                    Logger.DebugS("shipOwnership", $"Skipping deletion of shuttle {ToPrettyString(uid)} because an assigned crew member is online");
                    ownership.LastStatusChangeTime = _gameTiming.CurTime;
                    Dirty(uid, ownership);
                    continue;
                }

                // Queue ship for deletion
                _pendingDeletionShips.Add(uid);
            }
        }

        // Process deletions outside of enumeration
        foreach (var shipUid in _pendingDeletionShips)
        {
            if (!EntityManager.EntityExists(shipUid))
                continue;

            // Only handle deletion if this entity has a transform and is a grid
            if (TryComp<TransformComponent>(shipUid, out var transform) && transform.GridUid == shipUid)
            {
                Logger.InfoS("shipOwnership", $"Deleting abandoned ship {ToPrettyString(shipUid)}");

                _linkedLifecycleGrid.UnparentPlayersFromGrid(shipUid, true);
            }
        }

        _pendingDeletionShips.Clear();
    }

    private HashSet<string> GetOnlineCrewNames()
    {
        var onlineNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status is not (SessionStatus.Connected or SessionStatus.InGame)) continue;
            if (session.AttachedEntity is not { Valid: true } attached) continue;
            var name = Name(attached);
            if (!string.IsNullOrWhiteSpace(name)) onlineNames.Add(name);
        }
        return onlineNames;
    }

    private bool HasOnlineAssignedCrew(EntityUid shuttleUid, HashSet<string> onlineCrewNames)
    {
        if (onlineCrewNames.Count == 0) return false;
        var query = EntityQueryEnumerator<IdCardComponent, ShipCrewAssignmentComponent>();
        while (query.MoveNext(out var uid, out var id, out var assignment))
        {
            if (assignment.ShuttleUid != shuttleUid) continue;
            var assignedName = id.FullName ?? MetaData(uid).EntityName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(assignedName)) continue;
            if (onlineCrewNames.Contains(assignedName)) return true;
        }
        return false;
    }

    private void OnShipOwnershipStartup(EntityUid uid, ShipOwnershipComponent component, ComponentStartup args)
    {
        // If player is already online, mark them as such
        if (_playerManager.TryGetSessionById(component.OwnerUserId, out var player))
        {
            component.IsOwnerOnline = true;
            component.LastStatusChangeTime = _gameTiming.CurTime;
            Dirty(uid, component);
        }
    }

    private void OnShipOwnershipShutdown(EntityUid uid, ShipOwnershipComponent component, ComponentShutdown args)
    {
        // Nothing to do here for now
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.Session == null)
            return;

        var userId = e.Session.UserId;
        var query = EntityQueryEnumerator<ShipOwnershipComponent>();

        // Update all ships owned by this player
        while (query.MoveNext(out var shipUid, out var ownership))
        {
            if (ownership.OwnerUserId != userId)
                continue;

            switch (e.NewStatus)
            {
                case SessionStatus.Connected:
                case SessionStatus.InGame:
                    // Player has connected, update ownership
                    ownership.IsOwnerOnline = true;
                    ownership.LastStatusChangeTime = _gameTiming.CurTime;
                    Logger.DebugS("shipOwnership", $"Owner of ship {ToPrettyString(shipUid)} has connected");
                    break;

                case SessionStatus.Disconnected:
                    // Player has disconnected, update ownership
                    ownership.IsOwnerOnline = false;
                    ownership.LastStatusChangeTime = _gameTiming.CurTime;
                    Logger.DebugS("shipOwnership", $"Owner of ship {ToPrettyString(shipUid)} has disconnected");
                    break;
            }

            Dirty(shipUid, ownership);
        }
    }
}
