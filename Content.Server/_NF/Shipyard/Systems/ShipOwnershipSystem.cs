using Content.Server._Lua.Shipyard.Components;
using Content.Server._Lua.Stargate.Components;
using Content.Server.StationEvents.Events;
using Content.Server.Shuttles.Components;
using Content.Server.Power.Components;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared.Power;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._NF.Shipyard.Systems;

public sealed class ShipOwnershipSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly LinkedLifecycleGridSystem _linkedLifecycleGrid = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    private readonly HashSet<EntityUid> _pendingDeletionShips = new();
    private readonly HashSet<Entity<ShuttleConsoleComponent>> _shuttleConsoles = new();

    private TimeSpan _nextDeletionCheckTime;
    private const int DeletionCheckIntervalSeconds = 60;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShipOwnershipComponent, ComponentStartup>(OnShipOwnershipStartup);

        _nextDeletionCheckTime = _gameTiming.CurTime;
    }

    public void RegisterShipOwnership(EntityUid gridUid, ICommonSession owningPlayer)
    {
        if (!Exists(gridUid))
            return;

        var comp = EnsureComp<ShipOwnershipComponent>(gridUid);
        comp.OwnerUserId = owningPlayer.UserId;
        ResetDeletionTimer(comp);

        Dirty(gridUid, comp);

        Log.Info($"Registered ship {ToPrettyString(gridUid)} to player {owningPlayer.Name} ({owningPlayer.UserId})");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_gameTiming.CurTime < _nextDeletionCheckTime)
            return;

        _nextDeletionCheckTime = _gameTiming.CurTime + TimeSpan.FromSeconds(DeletionCheckIntervalSeconds);

        Log.Debug("Checking for abandoned ships to delete");

        var query = EntityQueryEnumerator<ShipOwnershipComponent>();
        while (query.MoveNext(out var uid, out var ownership))
        {
            if (!TryComp<MapGridComponent>(uid, out _))
            {
                continue;
            }

            if (HasComp<ParkedShuttleComponent>(uid))
            {
                StopDeletionTimer(uid, ownership, "shuttle is parked", resetElapsed: true);
                continue;
            }

            if (Transform(uid).MapUid is { } mapUid && HasComp<StargateDestinationComponent>(mapUid))
            {
                StopDeletionTimer(uid, ownership, "shuttle is in StarGate world", resetElapsed: true);
                continue;
            }

            var powered = IsShuttlePowered(uid);
            if (powered)
            {
                StopDeletionTimer(uid, ownership, "shuttle is powered", resetElapsed: true);
                continue;
            }

            if (HasPlayersOnShip(uid))
            {
                PauseDeletionTimer(uid, ownership, "players are aboard");
                continue;
            }

            ResumeDeletionTimer(uid, ownership);

            var timeout = TimeSpan.FromSeconds(ownership.DeletionTimeoutSeconds);
            if (ownership.AccumulatedUnpoweredTime >= timeout)
            {
                Log.Info($"Queueing abandoned ship {ToPrettyString(uid)} for deletion. elapsed={ownership.AccumulatedUnpoweredTime.TotalMinutes:F1}m timeout={ownership.DeletionTimeoutSeconds:F0}s");
                _pendingDeletionShips.Add(uid);
                continue;
            }

            var remaining = timeout - ownership.AccumulatedUnpoweredTime;
            Log.Debug($"Ship {ToPrettyString(uid)} not yet eligible for deletion. elapsed={ownership.AccumulatedUnpoweredTime.TotalMinutes:F1}m remaining={remaining.TotalMinutes:F1}m");
        }

        foreach (var shipUid in _pendingDeletionShips)
        {
            if (!Exists(shipUid))
                continue;

            if (Transform(shipUid).GridUid == shipUid)
            {
                Log.Info($"Deleting abandoned ship {ToPrettyString(shipUid)}");
                _linkedLifecycleGrid.UnparentPlayersFromGrid(shipUid, true);
            }
        }

        _pendingDeletionShips.Clear();
    }

    private bool IsShuttlePowered(EntityUid shuttleUid)
    {
        _shuttleConsoles.Clear();
        _lookup.GetGridEntities(shuttleUid, _shuttleConsoles);

        var totalConsoles = 0;
        var poweredConsoles = 0;

        foreach (var console in _shuttleConsoles)
        {
            if (!TryComp<ApcPowerReceiverComponent>(console, out var power))
                continue;

            totalConsoles++;
            if (power.Powered)
                poweredConsoles++;
        }

        if (totalConsoles == 0)
            return false;

        return poweredConsoles > 0;
    }

    private bool HasPlayersOnShip(EntityUid shuttleUid)
    {
        var query = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var transform))
        {
            if (transform.GridUid == shuttleUid)
                return true;
        }

        return false;
    }

    private void OnShipOwnershipStartup(EntityUid uid, ShipOwnershipComponent component, ComponentStartup args)
    {
        ResetDeletionTimer(component);
        Dirty(uid, component);
    }

    private void PauseDeletionTimer(EntityUid shipUid, ShipOwnershipComponent ownership, string reason)
    {
        if (ownership.IsDeletionTimerRunning)
        {
            ownership.AccumulatedUnpoweredTime += _gameTiming.CurTime - ownership.DeletionTimerStartTime;
            ownership.IsDeletionTimerRunning = false;
        }

        if (ownership.IsDeletionTimerPaused)
            return;

        ownership.IsDeletionTimerPaused = true;
        Dirty(shipUid, ownership);
        Log.Debug($"Paused abandonment timer for ship {ToPrettyString(shipUid)} because {reason}");
    }

    private void ResumeDeletionTimer(EntityUid shipUid, ShipOwnershipComponent ownership)
    {
        if (!ownership.IsDeletionTimerRunning)
        {
            ownership.IsDeletionTimerRunning = true;
            ownership.IsDeletionTimerPaused = false;
            ownership.DeletionTimerStartTime = _gameTiming.CurTime;
            Dirty(shipUid, ownership);
            Log.Debug($"Started abandonment timer for ship {ToPrettyString(shipUid)} ({ownership.DeletionTimeoutSeconds:F0}s)");
            return;
        }

        ownership.AccumulatedUnpoweredTime += _gameTiming.CurTime - ownership.DeletionTimerStartTime;
        ownership.DeletionTimerStartTime = _gameTiming.CurTime;
        Dirty(shipUid, ownership);
    }

    private void StopDeletionTimer(EntityUid shipUid, ShipOwnershipComponent ownership, string reason, bool resetElapsed)
    {
        if (!ownership.IsDeletionTimerRunning && !ownership.IsDeletionTimerPaused && !resetElapsed)
            return;

        if (resetElapsed)
            ownership.AccumulatedUnpoweredTime = TimeSpan.Zero;

        ownership.IsDeletionTimerRunning = false;
        ownership.IsDeletionTimerPaused = false;
        ownership.DeletionTimerStartTime = TimeSpan.Zero;
        Dirty(shipUid, ownership);
        Log.Debug($"Stopped abandonment timer for ship {ToPrettyString(shipUid)} because {reason}");
    }

    private void ResetDeletionTimer(ShipOwnershipComponent ownership)
    {
        ownership.IsDeletionTimerRunning = false;
        ownership.IsDeletionTimerPaused = false;
        ownership.DeletionTimerStartTime = TimeSpan.Zero;
        ownership.AccumulatedUnpoweredTime = TimeSpan.Zero;
    }
}
