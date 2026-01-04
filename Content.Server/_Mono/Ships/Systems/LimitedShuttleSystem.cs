using Content.Server.Power.Components;
using Content.Server.Shuttles.Components;
using Content.Shared._Mono.Ships.Components;
using Content.Shared._Mono.Shipyard;
using Content.Shared._NF.Shipyard;
using Content.Shared._NF.Shipyard.Prototypes;
using Content.Shared.Lua.CLVar;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Ships.Systems;

/// <summary>
/// This handles shuttles with a limit.
/// </summary>
public sealed class LimitedShuttleSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly ShuttleDeedSystem _shuttleDeed = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private TimeSpan _lastUpdate = TimeSpan.Zero;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(1);

    private const double PoweredInactivityThreshold = 0.5;

    private bool _useExistenceCheck = false;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AttemptShipyardShuttlePurchaseEvent>(OnAttemptShuttlePurchase);
        SubscribeLocalEvent<VesselComponent, ShipyardShuttlePurchaseEvent>(OnShuttlePurchase);

        _cfg.OnValueChanged(CLVars.ShipLimitCheckExistence, OnExistenceCheckChanged, true);
    }

    private void OnExistenceCheckChanged(bool useExistenceCheck)
    {
        _useExistenceCheck = useExistenceCheck;
    }

    private void OnShuttlePurchase(Entity<VesselComponent> ent, ref ShipyardShuttlePurchaseEvent args)
    {
        EnsureComp<ShipActivityComponent>(ent);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<VesselComponent>();

        if (_lastUpdate + _interval > _gameTiming.CurTime)
            return;

        _lastUpdate = _gameTiming.CurTime;

        while (query.MoveNext(out var uid, out _))
        {
            var inactivity = EnsureComp<ShipActivityComponent>(uid);

            // В режиме проверки существования не обновляем состояние активности
            if (_useExistenceCheck)
                continue;

            if (inactivity.LastChecked + inactivity.CheckInterval > _gameTiming.CurTime)
                continue;

            inactivity.LastChecked = _gameTiming.CurTime;

            var isActive = IsActive(uid);

            if (isActive && inactivity.TimesInactive > 0)
                inactivity.TimesInactive = 0;

            if (!isActive)
                inactivity.TimesInactive++;

            inactivity.InactiveLastCheck = !isActive;

            if (!isActive && inactivity.GetMinutesInactive() >= inactivity.InactiveThresholdMinutes)
                inactivity.InactivePastThreshold = true;

            Dirty(uid, inactivity);
        }
    }

    public bool CanPurchaseVessel(VesselPrototype vessel)
    {
        if (vessel.LimitActive <= 0)
            return true;

        var query = EntityQueryEnumerator<VesselComponent>();
        var shuttleCount = 0;

        while (query.MoveNext(out var uid, out var targetVessel))
        {
            if (targetVessel.VesselId != vessel.ID)
                continue;
            if (_useExistenceCheck)
            { shuttleCount++; }
            else
            {
                if (!TryComp<ShipActivityComponent>(uid, out var inactivity) || !inactivity.InactivePastThreshold)
                {
                    shuttleCount++;
                }
            }
        }

        return shuttleCount < vessel.LimitActive;
    }

    private void OnAttemptShuttlePurchase(ref AttemptShipyardShuttlePurchaseEvent ev)
    {
        if (!CanPurchaseVessel(ev.Vessel))
        {
            ev.CancelReason = "shipyard-console-limited";
            ev.Cancel();
        }
    }

    private bool IsActive(Entity<VesselComponent?> vessel)
    {
        var consoles = new HashSet<Entity<ShuttleConsoleComponent>>();
        _lookup.GetGridEntities(vessel.Owner, consoles);

        var totalPowerEntities = 0;
        var powered = 0;

        // If the deed has no owner or the ship has no consoles, it's inactive.
        if (!_shuttleDeed.HasOwner(vessel.Owner)
            || consoles.Count == 0)
            return false;

        foreach (var ent in consoles)
        {
            if (!TryComp<ApcPowerReceiverComponent>(ent, out var powerReceiver))
                continue;

            if (powerReceiver.Powered) // should be powered even if not switched on.
                powered++;

            totalPowerEntities++;
        }

        var percentage = totalPowerEntities != 0 && powered != 0 ? (double) powered / (double) totalPowerEntities : 0.0;

        if (percentage >= PoweredInactivityThreshold)
            return true;

        return false;
    }
}
