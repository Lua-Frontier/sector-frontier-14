using Content.Server._Lua.Shipyard.Components;
using Content.Server._Lua.Shipyard.Systems;
using Content.Server._Lua.StationRecords.Components;
using Content.Server._Lua.StationRecords.Systems;
using Content.Server.Mind;
using Content.Server.StationEvents.Events;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared.Access.Components;
using Content.Shared.Mind;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._NF.Shipyard.Systems;

/// <summary>
/// Manages ship ownership and handles cleanup of ships when owners are offline too long
/// </summary>
public sealed class ShipOwnershipSystem : EntitySystem
{
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly LinkedLifecycleGridSystem _linkedLifecycleGrid = default!;
    [Dependency] private readonly ShuttleParkingSystem _parking = default!;
    [Dependency] private readonly ShipCrewAssignmentSystem _shipCrew = default!;

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

        SubscribeLocalEvent<ParkedShuttleComponent, ComponentRemove>(OnShuttleUnparked);

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
        comp.IsDeletionTimerRunning = false;
        comp.DeletionTimerStartTime = TimeSpan.Zero;

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

        var onlineCrewUserIds = GetOnlineCrewUserIds();

        // Check for ships that need to be deleted due to owner absence
        var query = EntityQueryEnumerator<ShipOwnershipComponent>();
        while (query.MoveNext(out var uid, out var ownership))
        {
            // Skip ships with online owners
            if (ownership.IsOwnerOnline)
            {
                StopDeletionTimer(uid, ownership, "owner is online");
                continue;
            }

            var timeoutSeconds = TimeSpan.FromSeconds(ownership.DeletionTimeoutSeconds);
            if (_parking.IsParked(uid))
            {
                StopDeletionTimer(uid, ownership, "shuttle is parked");
                Logger.DebugS("shipOwnership", $"Skipping deletion of parked shuttle {ToPrettyString(uid)}");
                continue;
            }

            var onlineAssignedCrew = GetOnlineAssignedCrewNames(uid, onlineCrewUserIds);
            if (onlineAssignedCrew.Count > 0)
            {
                StopDeletionTimer(uid, ownership, $"assigned crew online: {string.Join(", ", onlineAssignedCrew)}");
                Logger.WarningS("shipOwnership", $"Skipping deletion of shuttle {ToPrettyString(uid)} because assigned crew are online: {string.Join(", ", onlineAssignedCrew)}");
                continue;
            }

            if (!ownership.IsDeletionTimerRunning)
            {
                StartDeletionTimer(uid, ownership);
                continue;
            }

            var countdownTime = _gameTiming.CurTime - ownership.DeletionTimerStartTime;
            if (countdownTime >= timeoutSeconds)
            {
                Logger.InfoS("shipOwnership", $"Queueing abandoned ship {ToPrettyString(uid)} for deletion. countdown={countdownTime.TotalMinutes:F1}m timeout={ownership.DeletionTimeoutSeconds:F0}s");
                _pendingDeletionShips.Add(uid);
                continue;
            }

            var remaining = timeoutSeconds - countdownTime;
            Logger.DebugS("shipOwnership", $"Ship {ToPrettyString(uid)} not yet eligible for deletion. countdown={countdownTime.TotalMinutes:F1}m remaining={remaining.TotalMinutes:F1}m");
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
                var clearedAssignments = _shipCrew.ClearAllForShuttle(shipUid);
                if (clearedAssignments > 0)
                    Logger.InfoS("shipOwnership", $"Cleared {clearedAssignments} crew assignment(s) for abandoned ship {ToPrettyString(shipUid)}");
                _linkedLifecycleGrid.UnparentPlayersFromGrid(shipUid, true);
            }
        }

        _pendingDeletionShips.Clear();
    }

    private HashSet<NetUserId> GetOnlineCrewUserIds()
    {
        var onlineUsers = new HashSet<NetUserId>();
        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status is not (SessionStatus.Connected or SessionStatus.InGame))
                continue;

            if (!_mind.TryGetMind(session.UserId, out _, out var mind) || mind.UserId != session.UserId)
            {
                continue;
            }

            onlineUsers.Add(session.UserId);
        }
        return onlineUsers;
    }

    private void OnShipOwnershipStartup(EntityUid uid, ShipOwnershipComponent component, ComponentStartup args)
    {
        // If player is already online, mark them as such
        if (_playerManager.TryGetSessionById(component.OwnerUserId, out var player))
        {
            component.IsOwnerOnline = true;
            component.LastStatusChangeTime = _gameTiming.CurTime;
            component.IsDeletionTimerRunning = false;
            component.DeletionTimerStartTime = TimeSpan.Zero;
            Dirty(uid, component);
        }
    }

    private void OnShipOwnershipShutdown(EntityUid uid, ShipOwnershipComponent component, ComponentShutdown args)
    {
        // Nothing to do here for now
    }

    private void OnShuttleUnparked(EntityUid uid, ParkedShuttleComponent component, ref ComponentRemove args)
    {
        if (!TryComp<ShipOwnershipComponent>(uid, out var ownership)) return;
        ownership.IsDeletionTimerRunning = false;
        ownership.DeletionTimerStartTime = TimeSpan.Zero;
        Dirty(uid, ownership);
        Logger.DebugS("shipOwnership", $"Shuttle {ToPrettyString(uid)} was unparked; abandonment timer reset");
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
                    ownership.IsDeletionTimerRunning = false;
                    ownership.DeletionTimerStartTime = TimeSpan.Zero;
                    Logger.DebugS("shipOwnership", $"Owner of ship {ToPrettyString(shipUid)} has connected; abandonment timer reset");
                    break;

                case SessionStatus.Disconnected:
                    // Player has disconnected, update ownership
                    ownership.IsOwnerOnline = false;
                    ownership.LastStatusChangeTime = _gameTiming.CurTime;
                    ownership.IsDeletionTimerRunning = false;
                    ownership.DeletionTimerStartTime = TimeSpan.Zero;
                    Logger.DebugS("shipOwnership", $"Owner of ship {ToPrettyString(shipUid)} has disconnected; waiting for abandonment conditions before starting timer");
                    break;
            }

            Dirty(shipUid, ownership);
        }
    }

    private List<string> GetOnlineAssignedCrewNames(EntityUid shuttleUid, HashSet<NetUserId> onlineCrewUserIds)
    {
        var matches = new List<string>();
        if (onlineCrewUserIds.Count == 0)
            return matches;

        var query = EntityQueryEnumerator<IdCardComponent, ShipCrewAssignmentComponent>();
        while (query.MoveNext(out var uid, out var id, out var assignment))
        {
            if (assignment.ShuttleUid != shuttleUid)
                continue;

            _shipCrew.ForceRefreshAssignmentIdentity(uid, assignment);

            if (assignment.AssignedUserId is not { } assignedUserId)
                continue;

            var assignedName = id.FullName ?? MetaData(uid).EntityName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(assignedName))
                continue;

            if (onlineCrewUserIds.Contains(assignedUserId))
                matches.Add(assignedName);
        }

        matches.Sort(StringComparer.OrdinalIgnoreCase);
        return matches;
    }

    private void StartDeletionTimer(EntityUid shipUid, ShipOwnershipComponent ownership)
    {
        if (ownership.IsDeletionTimerRunning)
            return;

        ownership.IsDeletionTimerRunning = true;
        ownership.DeletionTimerStartTime = _gameTiming.CurTime;
        Dirty(shipUid, ownership);
        Logger.DebugS("shipOwnership", $"Started abandonment timer for ship {ToPrettyString(shipUid)} ({ownership.DeletionTimeoutSeconds:F0}s)");
    }

    private void StopDeletionTimer(EntityUid shipUid, ShipOwnershipComponent ownership, string reason)
    {
        if (!ownership.IsDeletionTimerRunning)
            return;

        ownership.IsDeletionTimerRunning = false;
        ownership.DeletionTimerStartTime = TimeSpan.Zero;
        Dirty(shipUid, ownership);
        Logger.DebugS("shipOwnership", $"Stopped abandonment timer for ship {ToPrettyString(shipUid)} because {reason}");
    }
}
