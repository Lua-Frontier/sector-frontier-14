using Content.Server._Lua.Shipyard.Components;
using Content.Server._Lua.Shipyard.Systems;
using Content.Server._Lua.StationRecords.Components;
using Content.Server._Lua.StationRecords.Systems;
using Content.Server.StationEvents.Events;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared.Access.Components;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._NF.Shipyard.Systems;

public sealed class ShipOwnershipSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly LinkedLifecycleGridSystem _linkedLifecycleGrid = default!;
    [Dependency] private readonly ShuttleParkingSystem _parking = default!;
    [Dependency] private readonly ShipCrewAssignmentSystem _shipCrew = default!;

    private readonly HashSet<EntityUid> _pendingDeletionShips = new();

    private TimeSpan _nextDeletionCheckTime;
    private const int DeletionCheckIntervalSeconds = 60;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShipOwnershipComponent, ComponentStartup>(OnShipOwnershipStartup);
        SubscribeLocalEvent<ShipOwnershipComponent, ComponentShutdown>(OnShipOwnershipShutdown);
        SubscribeLocalEvent<ParkedShuttleComponent, ComponentRemove>(OnShuttleUnparked);

        _nextDeletionCheckTime = _gameTiming.CurTime;
    }

    public void RegisterShipOwnership(EntityUid gridUid, ICommonSession owningPlayer)
    {
        if (!EntityManager.EntityExists(gridUid))
            return;

        var comp = EnsureComp<ShipOwnershipComponent>(gridUid);
        comp.OwnerUserId = owningPlayer.UserId;
        comp.IsDeletionTimerRunning = false;
        comp.DeletionTimerStartTime = TimeSpan.Zero;

        Dirty(gridUid, comp);

        Logger.InfoS("shipOwnership", $"Registered ship {ToPrettyString(gridUid)} to player {owningPlayer.Name} ({owningPlayer.UserId})");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_gameTiming.CurTime < _nextDeletionCheckTime)
            return;

        _nextDeletionCheckTime = _gameTiming.CurTime + TimeSpan.FromSeconds(DeletionCheckIntervalSeconds);

        Logger.DebugS("shipOwnership", "Checking for abandoned ships to delete");

        var onlineUserIds = GetOnlineUserIds();

        var query = EntityQueryEnumerator<ShipOwnershipComponent>();
        while (query.MoveNext(out var uid, out var ownership))
        {
            if (onlineUserIds.Contains(ownership.OwnerUserId))
            {
                StopDeletionTimer(uid, ownership, "owner is online");
                continue;
            }

            if (_parking.IsParked(uid))
            {
                StopDeletionTimer(uid, ownership, "shuttle is parked");
                Logger.DebugS("shipOwnership", $"Skipping deletion of parked shuttle {ToPrettyString(uid)}");
                continue;
            }

            var onlineCrewNames = GetOnlineCrewNames(uid, onlineUserIds);
            if (onlineCrewNames.Count > 0)
            {
                StopDeletionTimer(uid, ownership, $"assigned crew online: {string.Join(", ", onlineCrewNames)}");
                Logger.WarningS("shipOwnership", $"Skipping deletion of shuttle {ToPrettyString(uid)} because assigned crew are online: {string.Join(", ", onlineCrewNames)}");
                continue;
            }

            if (!ownership.IsDeletionTimerRunning)
            {
                StartDeletionTimer(uid, ownership);
                continue;
            }

            var elapsed = _gameTiming.CurTime - ownership.DeletionTimerStartTime;
            var timeout = TimeSpan.FromSeconds(ownership.DeletionTimeoutSeconds);

            if (elapsed >= timeout)
            {
                Logger.InfoS("shipOwnership", $"Queueing abandoned ship {ToPrettyString(uid)} for deletion. countdown={elapsed.TotalMinutes:F1}m timeout={ownership.DeletionTimeoutSeconds:F0}s");
                _pendingDeletionShips.Add(uid);
                continue;
            }

            var remaining = timeout - elapsed;
            Logger.DebugS("shipOwnership", $"Ship {ToPrettyString(uid)} not yet eligible for deletion. countdown={elapsed.TotalMinutes:F1}m remaining={remaining.TotalMinutes:F1}m");
        }

        foreach (var shipUid in _pendingDeletionShips)
        {
            if (!EntityManager.EntityExists(shipUid))
                continue;

            if (TryComp<TransformComponent>(shipUid, out var transform) && transform.GridUid == shipUid)
            {
                Logger.InfoS("shipOwnership", $"Deleting abandoned ship {ToPrettyString(shipUid)}");
                var cleared = _shipCrew.ClearAllForShuttle(shipUid);
                if (cleared > 0)
                    Logger.InfoS("shipOwnership", $"Cleared {cleared} crew assignment(s) for abandoned ship {ToPrettyString(shipUid)}");
                _linkedLifecycleGrid.UnparentPlayersFromGrid(shipUid, true);
            }
        }

        _pendingDeletionShips.Clear();
    }

    private HashSet<NetUserId> GetOnlineUserIds()
    {
        var online = new HashSet<NetUserId>();
        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status is SessionStatus.Connected or SessionStatus.InGame)
                online.Add(session.UserId);
        }
        return online;
    }

    private List<string> GetOnlineCrewNames(EntityUid shuttleUid, HashSet<NetUserId> onlineUserIds)
    {
        var matches = new List<string>();
        if (onlineUserIds.Count == 0)
            return matches;

        var query = EntityQueryEnumerator<IdCardComponent, ShipCrewAssignmentComponent>();
        while (query.MoveNext(out var uid, out var id, out var assignment))
        {
            if (assignment.ShuttleUid != shuttleUid)
                continue;

            _shipCrew.ForceRefreshAssignmentIdentity(uid, assignment);

            if (assignment.AssignedUserId is not { } assignedUserId)
                continue;

            if (!onlineUserIds.Contains(assignedUserId))
                continue;

            var name = id.FullName ?? MetaData(uid).EntityName ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(name))
                matches.Add(name);
        }

        matches.Sort(StringComparer.OrdinalIgnoreCase);
        return matches;
    }

    private void OnShipOwnershipStartup(EntityUid uid, ShipOwnershipComponent component, ComponentStartup args)
    {
        component.IsDeletionTimerRunning = false;
        component.DeletionTimerStartTime = TimeSpan.Zero;
        Dirty(uid, component);
    }

    private void OnShipOwnershipShutdown(EntityUid uid, ShipOwnershipComponent component, ComponentShutdown args)
    {
    }

    private void OnShuttleUnparked(EntityUid uid, ParkedShuttleComponent component, ref ComponentRemove args)
    {
        if (!TryComp<ShipOwnershipComponent>(uid, out var ownership))
            return;

        ownership.IsDeletionTimerRunning = false;
        ownership.DeletionTimerStartTime = TimeSpan.Zero;
        Dirty(uid, ownership);

        Logger.DebugS("shipOwnership", $"Shuttle {ToPrettyString(uid)} was unparked; abandonment timer reset");
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
