using Content.Server._Mono.AmmoLoader;
using Content.Server._Mono.FireControl;
using Content.Shared.DeviceLinking.Events;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Mono.AmmoLoader;
using Content.Shared._Mono.ShipGuns;
using Content.Shared._Mono.SpaceArtillery;
using Content.Shared.Camera;
using Content.Shared.Examine;
using Content.Shared.Power;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Player;
using SpaceArtilleryComponent = Content.Server._Mono.SpaceArtillery.Components.SpaceArtilleryComponent;
using Content.Shared.Power.Components;

namespace Content.Server._Mono.SpaceArtillery;

public sealed partial class SpaceArtillerySystem : EntitySystem
{
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _recoilSystem = default!;
    [Dependency] private readonly FireControlSystem _fireControl = default!;
    [Dependency] private readonly AmmoLoaderSystem _ammoLoader = default!;

    private const float BIG_DAMAGE = 1000;
    private const float BIG_DAMGE_KICK = 35;
    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("SpaceArtillery");
        SubscribeLocalEvent<SpaceArtilleryComponent, BeforeCauseImpulseEvent>(OnBeforeCauseImpulse);
        SubscribeLocalEvent<SpaceArtilleryComponent, AmmoShotEvent>(OnShotEvent);
        SubscribeLocalEvent<SpaceArtilleryComponent, PowerChangedEvent>(OnApcChanged);
        SubscribeLocalEvent<SpaceArtilleryComponent, OnEmptyGunShotEvent>(OnEmptyShotEvent);
        SubscribeLocalEvent<SpaceArtilleryComponent, SignalReceivedEvent>(OnSignalReceived);
        SubscribeLocalEvent<SpaceArtilleryComponent, ChargeChangedEvent>(OnBatteryChargeChanged);
        SubscribeLocalEvent<ShipWeaponProjectileComponent, ProjectileHitEvent>(OnProjectileHit);
        SubscribeLocalEvent<ShipGunClassComponent, ExaminedEvent>(OnExamined);
    }


    private void OnSignalReceived(EntityUid uid, SpaceArtilleryComponent component, ref SignalReceivedEvent args)
    {
        if (args.Port != component.SpaceArtilleryLoadPort)
            return;

        if (TryComp<AmmoLoaderComponent>(args.Trigger, out var loader) && args.Trigger != null)
            _ammoLoader.TryTransferAmmoTo(new Entity<AmmoLoaderComponent>(args.Trigger.Value, loader), uid);
    }


    private void OnApcChanged(EntityUid uid, SpaceArtilleryComponent component, ref PowerChangedEvent args)
    {
        if (TryComp<BatterySelfRechargerComponent>(uid, out var batteryCharger))
        {
            if (args.Powered)
            {
                batteryCharger.AutoRecharge = true;
                batteryCharger.AutoRechargeRate = component.PowerChargeRate;
            }
            else
            {
                batteryCharger.AutoRecharge = true;
                batteryCharger.AutoRechargeRate = component.PowerUsePassive * -1;
            }
        }
    }


    private void OnBatteryChargeChanged(EntityUid uid, SpaceArtilleryComponent component, ref ChargeChangedEvent args)
    {
        if (TryComp<ApcPowerReceiverComponent>(uid, out var apcPowerReceiver) && TryComp<BatteryComponent>(uid, out var battery))
        {
            apcPowerReceiver.Load = battery.CurrentCharge >= battery.MaxCharge * 0.99 ? component.PowerUsePassive : component.PowerUsePassive + component.PowerChargeRate;
        }
    }

    private void OnBeforeCauseImpulse(EntityUid uid, SpaceArtilleryComponent component, ref BeforeCauseImpulseEvent args)
    {
        args.Cancelled = true;
    }

    private void OnShotEvent(EntityUid uid, SpaceArtilleryComponent component, AmmoShotEvent args)
    {
        if (args.FiredProjectiles.Count == 0)
        {
            OnMalfunction(uid, component);
            return;
        }

        if (TryComp<BatteryComponent>(uid, out var battery))
        {
            _battery.UseCharge(uid, component.PowerUseActive, battery);
        }
    }

    private void OnEmptyShotEvent(EntityUid uid, SpaceArtilleryComponent component, OnEmptyGunShotEvent args)
    {
        OnMalfunction(uid, component);
    }

    private void OnMalfunction(EntityUid uid, SpaceArtilleryComponent component)
    {
    }

    private void OnProjectileHit(EntityUid uid, ShipWeaponProjectileComponent component, ProjectileHitEvent hitEvent)
    {
        var grid = Transform(hitEvent.Target).GridUid;
        if (grid == null)
            return;

        var players = Filter.Empty();
        players.AddInGrid((EntityUid)grid);

        foreach (var player in players.Recipients)
        {
            if (player.AttachedEntity is not EntityUid playerEnt)
                continue;

            var vector = _xform.GetWorldPosition(uid) - _xform.GetWorldPosition(playerEnt);

            _recoilSystem.KickCamera(playerEnt, vector.Normalized() * (float)hitEvent.Damage.GetTotal() / BIG_DAMAGE * BIG_DAMGE_KICK);
        }
    }

    private void OnExamined(EntityUid uid, ShipGunClassComponent component, ExaminedEvent args)
    {
        if (!TryComp<FireControllableComponent>(uid, out var controllable))
            return;
        if (!args.IsInDetailsRange)
            return;
        args.PushMarkup(
            Loc.GetString(
                "ship-gun-class-component-examine-detail",
                ("processingPower", _fireControl.GetProcessingPowerCost(uid, controllable))
            )
        );
    }
}
