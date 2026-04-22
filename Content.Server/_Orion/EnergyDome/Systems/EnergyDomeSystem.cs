using Content.Server._Orion.EnergyDome.Components;
using Content.Server.DeviceLinking.Systems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.PowerCell;
using Content.Server.Emp;
using Content.Server._Chaos.EnergyDome.Components;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared.Timing;
using Content.Shared.Toggleable;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Content.Server.Damage.Systems;
namespace Content.Server._Orion.EnergyDome.Systems;

//
// License-Identifier: AGPL-3.0-or-later
//

public sealed class EnergyDomeSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly DeviceLinkSystem _signalSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!; // CS-Tweak
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!; // CS-Tweak
    [Dependency] private readonly DamageableSystem _damageable = default!; // CS-Tweak

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EnergyDomeGeneratorComponent, MapInitEvent>(OnInit);

        SubscribeLocalEvent<EnergyDomeGeneratorComponent, ActivateInWorldEvent>(OnActivatedInWorld);
        SubscribeLocalEvent<EnergyDomeGeneratorComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<EnergyDomeGeneratorComponent, SignalReceivedEvent>(OnSignalReceived);
        SubscribeLocalEvent<EnergyDomeGeneratorComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<EnergyDomeGeneratorComponent, ToggleActionEvent>(OnToggleAction);

        SubscribeLocalEvent<EnergyDomeGeneratorComponent, PowerCellChangedEvent>(OnPowerCellChanged);
        SubscribeLocalEvent<EnergyDomeGeneratorComponent, PowerCellSlotEmptyEvent>(OnPowerCellSlotEmpty);
        SubscribeLocalEvent<EnergyDomeGeneratorComponent, ChargeChangedEvent>(OnChargeChanged);

        SubscribeLocalEvent<EnergyDomeGeneratorComponent, EntParentChangedMessage>(OnParentChanged);

        SubscribeLocalEvent<EnergyDomeGeneratorComponent, GetVerbsEvent<ActivationVerb>>(AddToggleDomeVerb);
        SubscribeLocalEvent<EnergyDomeGeneratorComponent, ExaminedEvent>(OnExamine);

        SubscribeLocalEvent<EnergyDomeGeneratorComponent, ComponentRemove>(OnComponentRemove);

        SubscribeLocalEvent<EnergyDomeComponent, DamageChangedEvent>(OnDomeDamaged);

        SubscribeLocalEvent<InteQShieldComponent, EmpPulseEvent>(OnEmpPulse); // CS-Tweak
    }

    // CS-Tweak start
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<InteQShieldComponent>();
        while (query.MoveNext(out var uid, out var shield))
        {
            UpdateShieldRegeneration(uid, shield, frameTime);
        }
    }
    // CS-Tweak end

    private void OnInit(Entity<EnergyDomeGeneratorComponent> generator, ref MapInitEvent args)
    {
        if (generator.Comp.CanDeviceNetworkUse)
            _signalSystem.EnsureSinkPorts(generator, generator.Comp.TogglePort, generator.Comp.OnPort, generator.Comp.OffPort);
    }

    #region Use Ways

    private void OnSignalReceived(Entity<EnergyDomeGeneratorComponent> generator, ref SignalReceivedEvent args)
    {
        if (!generator.Comp.CanDeviceNetworkUse)
            return;

        if (args.Port == generator.Comp.OnPort)
        {
            AttemptToggle(generator, true);
        }
        if (args.Port == generator.Comp.OffPort)
        {
            AttemptToggle(generator, false);
        }
        if (args.Port == generator.Comp.TogglePort)
        {
            AttemptToggle(generator, !generator.Comp.Enabled);
        }
    }

    private void OnAfterInteract(Entity<EnergyDomeGeneratorComponent> generator, ref AfterInteractEvent args)
    {
        if (generator.Comp.CanInteractUse)
            AttemptToggle(generator, !generator.Comp.Enabled);
    }

    private void OnActivatedInWorld(Entity<EnergyDomeGeneratorComponent> generator, ref ActivateInWorldEvent args)
    {
        if (generator.Comp.CanInteractUse)
            AttemptToggle(generator, !generator.Comp.Enabled);
    }

    private void OnExamine(Entity<EnergyDomeGeneratorComponent> generator, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(
            (generator.Comp.Enabled)
            ? "energy-dome-on-examine-is-on-message"
            : "energy-dome-on-examine-is-off-message"
            ));
        
        // CS-Tweak start
        if (TryComp<InteQShieldComponent>(generator, out var shieldComp))
        {
            args.PushMarkup(Loc.GetString("inteq-shield-health", ("current", shieldComp.CurrentHealth), ("max", shieldComp.MaxHealth)));
            if (shieldComp.OnCooldown)
            {
                var remaining = (shieldComp.CooldownEndTime - _timing.CurTime).TotalSeconds;
                if (remaining < 0 || remaining == null)
                {
                    remaining = 0;
                }
                args.PushMarkup(Loc.GetString("inteq-shield-cooldown", ("time", (int)Math.Max(0, remaining))));
            }
            if (shieldComp.EmpDisabled)
            {
                var remaining = (shieldComp.EmpEndTime - _timing.CurTime).TotalSeconds;
                if (remaining < 0 || remaining == null)
                {
                    remaining = 0;
                }
                args.PushMarkup(Loc.GetString("inteq-shield-emp-active", ("time", (int)Math.Max(0, remaining))));
            }
        }
        // CS-Tweak end
    }

    private void AddToggleDomeVerb(Entity<EnergyDomeGeneratorComponent> generator, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !generator.Comp.CanInteractUse)
            return;

        ActivationVerb verb = new()
        {
            Text = Loc.GetString("energy-dome-verb-toggle"),
            Act = () => AttemptToggle(generator, !generator.Comp.Enabled)
        };

        args.Verbs.Add(verb);
    }

    private static void OnGetActions(Entity<EnergyDomeGeneratorComponent> generator, ref GetItemActionsEvent args)
    {
        if (generator.Comp.CanInteractUse)
            args.AddAction(ref generator.Comp.ToggleActionEntity, generator.Comp.ToggleAction);
    }

    private void OnToggleAction(Entity<EnergyDomeGeneratorComponent> generator, ref ToggleActionEvent args)
    {
        if (args.Handled)
            return;

        AttemptToggle(generator, !generator.Comp.Enabled);
        args.Handled = true;
    }

    #endregion

    #region Interactions

    private void OnPowerCellSlotEmpty(Entity<EnergyDomeGeneratorComponent> generator, ref PowerCellSlotEmptyEvent args)
    {
        TurnOff(generator, true);
    }

    private void OnPowerCellChanged(Entity<EnergyDomeGeneratorComponent> generator, ref PowerCellChangedEvent args)
    {
        if (args.Ejected || !_powerCell.HasDrawCharge(generator))
            TurnOff(generator, true);
    }

    private void OnChargeChanged(Entity<EnergyDomeGeneratorComponent> generator, ref ChargeChangedEvent args)
    {
        if (args.Charge == 0)
            TurnOff(generator, true);
    }

    private void OnDomeDamaged(Entity<EnergyDomeComponent> dome, ref DamageChangedEvent args)
    {
        if (dome.Comp.Generator == null)
            return;

        if (args.DamageDelta == null)
            return;

        var generatorUid = dome.Comp.Generator.Value;
        if (!TryComp<EnergyDomeGeneratorComponent>(generatorUid, out var generatorComp))
            return;

        var totalDamage = args.DamageDelta.GetTotal().Float();

        // CS-Tweak start
        if (TryComp<InteQShieldComponent>(generatorUid, out var shieldComp))
        {
            var shieldDamage = new DamageSpecifier();
            foreach (var (damageType, amount) in args.DamageDelta.DamageDict)
            {
                if (damageType == shieldComp.MultiplyDamage)
                {
                    shieldDamage.DamageDict[damageType] = amount * 3; // умножаем определенный тип на 3
                }
                else
                {
                    shieldDamage.DamageDict[damageType] = amount; // пропорциональный урон для всего другого
                }
            }

            _damageable.TryChangeDamage(generatorUid, shieldDamage);

            totalDamage = shieldDamage.GetTotal().Float();

            shieldComp.CurrentHealth -= (int)totalDamage;
            shieldComp.LastDamageTime = _timing.CurTime; // записываем момент последнего попадания, для некст логики у КД
            _audio.PlayPvs(generatorComp.ParrySound, dome);

            // выключаем щит и запускаем кд, если здоровье щита упало до нуля
            if (shieldComp.CurrentHealth <= 0)
            {
                shieldComp.CurrentHealth = 0;
                TurnOff((generatorUid, generatorComp), true);
                shieldComp.CooldownEndTime = _timing.CurTime + TimeSpan.FromSeconds(shieldComp.CooldownTime);
                shieldComp.OnCooldown = !shieldComp.OnCooldown;
            }
        }
        else
        {
            // CS-Tweak end
            var energyLeak = totalDamage * generatorComp.DamageEnergyDraw;

            _audio.PlayPvs(generatorComp.ParrySound, dome);

            if (HasComp<PowerCellDrawComponent>(generatorUid))
            {
                if (_powerCell.TryGetBatteryFromSlot(generatorUid, out var cell))
                {
                    _battery.UseCharge(generatorUid, energyLeak, cell);

                    if (cell.CurrentCharge == 0)
                        TurnOff((generatorUid, generatorComp), true);
                }
            }

            // It seems to me it would not work well to hang both a powercell and an internal battery with wire charging on the object....
            if (!TryComp<BatteryComponent>(generatorUid, out var battery))
                return;

            _battery.UseCharge(generatorUid, energyLeak, battery);

            if (battery.CurrentCharge == 0)
                TurnOff((generatorUid, generatorComp), true);
        }
    }

    private void OnParentChanged(Entity<EnergyDomeGeneratorComponent> generator, ref EntParentChangedMessage args)
    {
        // TODO: taking the active barrier in hand for some reason does not manage to change the parent in this case,
        // and the barrier is not turned off.
        if (GetProtectedEntity(generator) != generator.Comp.DomeParentEntity)
            TurnOff(generator, false);
    }

    private void OnComponentRemove(Entity<EnergyDomeGeneratorComponent> generator, ref ComponentRemove args)
    {
        TurnOff(generator, false);
    }

    #endregion

    #region Functional

    public bool AttemptToggle(Entity<EnergyDomeGeneratorComponent> generator, bool status)
    {
        if (_useDelay.IsDelayed(generator.Owner))
        {
            _audio.PlayPvs(generator.Comp.TurnOffSound, generator);
            _popup.PopupEntity(
                    Loc.GetString("energy-dome-recharging"),
                    generator);
            return false;
        }

        // CS-Tweak start. Парочка проверок на наличие компонента и стейты его переменных
        if (TryComp<InteQShieldComponent>(generator, out var shieldComp))
        {
            if (shieldComp.EmpDisabled)
            {
                _audio.PlayPvs(generator.Comp.AccessDeniedSound, generator);
                _popup.PopupEntity(Loc.GetString("inteq-shield-emp-disabled"), generator);
                return false;
            }

            if (shieldComp.OnCooldown)
            {
                _audio.PlayPvs(generator.Comp.AccessDeniedSound, generator);
                _popup.PopupEntity(Loc.GetString("inteq-shield-on-cooldown"), generator);
                return false;
            }

            if (status && shieldComp.CurrentHealth < shieldComp.MinHealthToActivate)
            {
                _audio.PlayPvs(generator.Comp.AccessDeniedSound, generator);
                _popup.PopupEntity(Loc.GetString("inteq-shield-insufficient-health"), generator);
                return false;
            }
        }
        // CS-Tweak end

        if (TryComp<PowerCellSlotComponent>(generator, out _))
        {
            if (!_powerCell.TryGetBatteryFromSlot(generator, out _) && !TryComp(generator, out BatteryComponent? _))
            {
                _audio.PlayPvs(generator.Comp.TurnOffSound, generator);
                _popup.PopupEntity(
                    Loc.GetString("energy-dome-no-cell"),
                    generator);
                return false;
            }

            if (!_powerCell.HasDrawCharge(generator))
            {
                _audio.PlayPvs(generator.Comp.TurnOffSound, generator);
                _popup.PopupEntity(
                    Loc.GetString("energy-dome-no-power"),
                    generator);
                return false;
            }
        }

        if (TryComp<BatteryComponent>(generator, out var battery))
        {
            if (battery.CurrentCharge == 0)
            {
                _audio.PlayPvs(generator.Comp.TurnOffSound, generator);
                _popup.PopupEntity(
                    Loc.GetString("energy-dome-no-power"),
                    generator);
                return false;
            }
        }

        Toggle(generator, status);
        return true;
    }

    private void Toggle(Entity<EnergyDomeGeneratorComponent> generator, bool status)
    {
        if (status)
            TurnOn(generator);
        else
            TurnOff(generator, false);
    }

    private void TurnOn(Entity<EnergyDomeGeneratorComponent> generator)
    {
        if (generator.Comp.Enabled)
            return;

        var protectedEntity = GetProtectedEntity(generator);

        var newDome = Spawn(generator.Comp.DomePrototype, Transform(protectedEntity).Coordinates);
        generator.Comp.DomeParentEntity = protectedEntity;
        _transform.SetParent(newDome, protectedEntity);

        if (TryComp<EnergyDomeComponent>(newDome, out var domeComp))
        {
            domeComp.Generator = generator;
        }

        if (TryComp<PowerCellDrawComponent>(generator.Owner, out _))
        {
            _powerCell.SetDrawEnabled(generator.Owner, true);
        }

        if (TryComp<BatterySelfRechargerComponent>(generator, out var recharger))
        {
            recharger.AutoRecharge = true;
        }

        generator.Comp.SpawnedDome = newDome;
        _audio.PlayPvs(generator.Comp.TurnOnSound, generator);
        generator.Comp.Enabled = true;
    }

    private void TurnOff(Entity<EnergyDomeGeneratorComponent> generator, bool startReloading)
    {
        if (!generator.Comp.Enabled)
            return;

        generator.Comp.Enabled = false;
        QueueDel(generator.Comp.SpawnedDome);

        if (TryComp<PowerCellDrawComponent>(generator.Owner, out _))
        {
            _powerCell.SetDrawEnabled(generator.Owner, false);
        }
        if (TryComp<BatterySelfRechargerComponent>(generator, out var recharger))
        {
            recharger.AutoRecharge = false;
        }

        _audio.PlayPvs(generator.Comp.TurnOffSound, generator);

        if (!startReloading)
            return;

        _audio.PlayPvs(generator.Comp.EnergyOutSound, generator);

        if (TryComp<UseDelayComponent>(generator, out var useDelay))
        {
            _useDelay.TryResetDelay(new Entity<UseDelayComponent>(generator, useDelay));
        }
    }

    #endregion // CS-Tweak. забыли видимо добавить

    #region CS-Tweak пояс-щит

    private void OnEmpPulse(Entity<InteQShieldComponent> shield, ref EmpPulseEvent args)
    {
        // записываем время, когда закончится действие ЭМП, чтобы на это время отключить щит и не позволить его включить
        shield.Comp.EmpEndTime = _timing.CurTime + TimeSpan.FromSeconds(shield.Comp.EmpDisableTime);
        shield.Comp.EmpDisabled = !shield.Comp.EmpDisabled;

        // эмп выключает щит на время
        if (TryComp<EnergyDomeGeneratorComponent>(shield, out var generatorComp) && generatorComp.Enabled)
        {
            TurnOff((shield.Owner, generatorComp), true);
        }

        // разряд батареи в нулину при эмп
        if (_powerCell.TryGetBatteryFromSlot(shield.Owner, out var cell))
        {
            _battery.SetCharge(cell.Owner, 0, cell);
        }
    }

    private void UpdateShieldRegeneration(EntityUid uid, InteQShieldComponent shield, float frameTime)
    {
        var currentTime = _timing.CurTime;

        // проверяем активность эмп эффекта
        if (shield.EmpDisabled && shield.EmpEndTime <= currentTime)
        {
            shield.EmpEndTime = TimeSpan.FromSeconds(0);
            shield.EmpDisabled = !shield.EmpDisabled;
        }

        if (shield.EmpDisabled)
            return;

        // проверяем активность кд и сбрасываем если истёк
        if (shield.OnCooldown && shield.CooldownEndTime <= currentTime)
        {
            shield.CooldownEndTime = TimeSpan.FromSeconds(0);
            shield.OnCooldown = !shield.OnCooldown;
        }

        // проверяем заряд батареи
        if (_powerCell.TryGetBatteryFromSlot(uid, out var cell) && cell.CurrentCharge <= 0)
        {
            if (TryComp<EnergyDomeGeneratorComponent>(uid, out var generatorComp) && generatorComp.Enabled)
            {
                TurnOff((uid, generatorComp), true);
            }
            return;
        }

        // проверяем активность кд
        if (shield.LastDamageTime.HasValue && _timing.CurTime >= shield.LastDamageTime.Value + TimeSpan.FromSeconds(shield.CooldownTime))
        {
            // реген прочности щита
            var regenAmount = shield.RegenRate * frameTime;
            shield.AccumulatedRegen += regenAmount;

            int healthGained = 0;
            while (shield.AccumulatedRegen >= 1f && shield.CurrentHealth < shield.MaxHealth && cell != null && cell.CurrentCharge >= shield.RegenEnergyCost)
            {
                shield.CurrentHealth += 1;
                shield.AccumulatedRegen -= 1f;
                healthGained += 1;
            }

            var energyCost = (float)shield.RegenEnergyCost * healthGained;
            if (cell != null && energyCost > 0)
            {
                _battery.UseCharge(cell.Owner, energyCost, cell);
            }

            if (shield.CurrentHealth >= shield.MaxHealth)
            {
                shield.LastDamageTime = null; // сбрасываем время последнего повреждения, когда щит полностью восстановлен
            }
        }
    }
    // CS-Tweak end

    #endregion

    #region Util

    private EntityUid GetProtectedEntity(EntityUid entity)
    {
        return (_container.TryGetOuterContainer(entity, Transform(entity), out var container))
            ? container.Owner
            : entity;
    }

    #endregion
}
