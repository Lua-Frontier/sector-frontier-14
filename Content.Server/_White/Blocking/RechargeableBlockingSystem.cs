using Content.Server._Mono.Blocking;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.PowerCell;
using Content.Shared.Blocking;
using Content.Shared.Blocking.Components;
using Content.Shared._Mono.Blocking; // Mono
using Content.Shared._Mono.Blocking.Components;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.PowerCell.Components;

namespace Content.Server._White.Blocking;

public sealed class RechargeableBlockingSystem : SharedBlockingSystem // Mono
{
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly ShieldToggleSystem _shieldToggle = default!;
    [Dependency] private readonly ItemToggleSystem _itemToggle = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RechargeableBlockingComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<RechargeableBlockingComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<RechargeableBlockingComponent, ShieldBlockedDamageEvent>(OnShieldBlockedDamage);
        SubscribeLocalEvent<RechargeableBlockingComponent, ShieldReflectedDamageEvent>(OnShieldReflectedDamage);
        SubscribeLocalEvent<RechargeableBlockingComponent, ShieldToggleAttemptEvent>(OnShieldToggleAttempt);
        SubscribeLocalEvent<RechargeableBlockingComponent, ItemToggleActivateAttemptEvent>(OnItemToggleActivateAttempt);
        SubscribeLocalEvent<RechargeableBlockingComponent, ChargeChangedEvent>(OnChargeChanged);
        SubscribeLocalEvent<RechargeableBlockingComponent, PowerCellChangedEvent>(OnPowerCellChanged);
    }

    private void OnExamined(EntityUid uid, RechargeableBlockingComponent component, ExaminedEvent args)
    {
        if (!component.Discharged)
        {
            _powerCell.OnBatteryExamined(uid, null, args);
            return;
        }

        args.PushMarkup(Loc.GetString("rechargeable-blocking-discharged"));
        args.PushMarkup(Loc.GetString("rechargeable-blocking-remaining-time", ("remainingTime", GetRemainingTime(uid))));
    }

    private int GetRemainingTime(EntityUid uid)
    {
        if (!_battery.TryGetBatteryComponent(uid, out var batteryComponent, out var batteryUid)
            || !TryComp<BatterySelfRechargerComponent>(batteryUid, out var recharger)
            || recharger is not { AutoRechargeRate: > 0, AutoRecharge: true })
            return 0;

        return (int) MathF.Round((batteryComponent.MaxCharge - batteryComponent.CurrentCharge) /
                                 recharger.AutoRechargeRate);
    }

    private void OnDamageChanged(EntityUid uid, RechargeableBlockingComponent component, DamageChangedEvent args)
    {
        if (!TryComp<BlockingComponent>(uid, out var blocking) || !blocking.IsClothing)
            return;

        if (!_battery.TryGetBatteryComponent(uid, out var batteryComponent, out var batteryUid)
            || args.DamageDelta == null)
            return;

        if (!IsShieldEnabled(uid))
            return;

        var batteryUse = Math.Min(args.DamageDelta.GetTotal().Float(), batteryComponent.CurrentCharge);
        _battery.TryUseCharge(batteryUid.Value, batteryUse, batteryComponent);
    }

    private void OnShieldBlockedDamage(EntityUid uid, RechargeableBlockingComponent component, ref ShieldBlockedDamageEvent args)
    {
        if (!_battery.TryGetBatteryComponent(uid, out var batteryComponent, out var batteryUid))
            return;

        if (!IsShieldEnabled(uid))
            return;
        var batteryUseRaw = args.TotalBlockedDamage + args.BallisticBlockedDamage * 1.25f;
        var batteryUse = Math.Min(batteryUseRaw, batteryComponent.CurrentCharge);
        _battery.TryUseCharge(batteryUid.Value, batteryUse, batteryComponent);
    }

    private void OnShieldReflectedDamage(EntityUid uid, RechargeableBlockingComponent component, ref ShieldReflectedDamageEvent args)
    {
        if (!_battery.TryGetBatteryComponent(uid, out var batteryComponent, out var batteryUid))
            return;

        if (!IsShieldEnabled(uid))
            return;

        var amount = Math.Max(args.TotalReflectedDamage, 2f);
        var batteryUse = Math.Min(amount, batteryComponent.CurrentCharge);
        _battery.TryUseCharge(batteryUid.Value, batteryUse, batteryComponent);
    }

    private void OnShieldToggleAttempt(EntityUid uid, RechargeableBlockingComponent component, ref ShieldToggleAttemptEvent args)
    {
        if (!component.Discharged)
            return;

        _popup.PopupEntity(Loc.GetString("rechargeable-blocking-remaining-time-popup",
                ("remainingTime", GetRemainingTime(uid))),
            args.User ?? uid);
        args.Cancelled = true;
    }

    private void OnItemToggleActivateAttempt(EntityUid uid, RechargeableBlockingComponent component, ref ItemToggleActivateAttemptEvent args)
    {
        if (!component.Discharged)
            return;

        args.Popup = Loc.GetString("rechargeable-blocking-remaining-time-popup",
            ("remainingTime", GetRemainingTime(uid)));
        args.Cancelled = true;
    }

    private void OnChargeChanged(EntityUid uid, RechargeableBlockingComponent component, ChargeChangedEvent args)
    {
        CheckCharge(uid, component);
    }

    private void OnPowerCellChanged(EntityUid uid, RechargeableBlockingComponent component, PowerCellChangedEvent args)
    {
        CheckCharge(uid, component);
    }

    private void CheckCharge(EntityUid uid, RechargeableBlockingComponent component)
    {
        if (!_battery.TryGetBatteryComponent(uid, out var battery, out _))
            return;

        BatterySelfRechargerComponent? recharger;
        if (battery.CurrentCharge < 1)
        {
            if (TryComp(uid, out recharger))
                recharger.AutoRechargeRate = component.DischargedRechargeRate;

            component.Discharged = true;
            if (TryComp<ShieldToggleComponent>(uid, out var shieldComp))
            {
                _shieldToggle.SetEnabled(uid, shieldComp, false);
            }
            else if (TryComp<ItemToggleComponent>(uid, out _))
            {
                _itemToggle.TrySetActive((uid, null), false, predicted: false);
            }
            return;
        }

        if (battery.CurrentCharge < battery.MaxCharge)
            return;

        component.Discharged = false;
        if (TryComp(uid, out recharger))
                recharger.AutoRechargeRate = component.ChargedRechargeRate;
    }

    private bool IsShieldEnabled(EntityUid uid)
    {
        if (TryComp<ShieldToggleComponent>(uid, out var shieldComp))
            return shieldComp.Enabled;

        if (TryComp<ItemToggleComponent>(uid, out var toggleComp))
            return toggleComp.Activated;

        return false;
    }
}
