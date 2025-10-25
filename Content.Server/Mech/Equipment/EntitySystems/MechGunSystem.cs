using Content.Server.Mech.Systems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Random;
using Robust.Server.Containers;

namespace Content.Server.Mech.Equipment.EntitySystems;
public sealed class MechGunSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly MechSystem _mech = default!;
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly ContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MechEquipmentComponent, GunShotEvent>(MechGunShot);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<MechEquipmentComponent, BatteryComponent>();
        while (query.MoveNext(out var uid, out var mechEquipment, out var battery))
        {
            if (!mechEquipment.EquipmentOwner.HasValue) continue;
            if (!TryComp<MechComponent>(mechEquipment.EquipmentOwner.Value, out var mech)) continue;
            if (battery.CurrentCharge >= battery.MaxCharge || mech.Energy <= 0) continue;
            var chargeRate = 10f * frameTime;
            var chargeDelta = Math.Min(chargeRate, battery.MaxCharge - battery.CurrentCharge);
            chargeDelta = Math.Min(chargeDelta, mech.Energy.Float());
            if (chargeDelta <= 0) continue;
            if (_mech.TryChangeEnergy(mechEquipment.EquipmentOwner.Value, -chargeDelta, mech))
            { _battery.SetCharge(uid, battery.CurrentCharge + chargeDelta, battery); }
        }
    }

    private void MechGunShot(EntityUid uid, MechEquipmentComponent component, ref GunShotEvent args)
    {
        if (!component.EquipmentOwner.HasValue)
            return;

        if (!TryComp<MechComponent>(component.EquipmentOwner.Value, out var mech))
            return;

        if (TryComp<BatteryComponent>(uid, out var battery))
        {
            ChargeGunBattery(uid, battery);
            return;
        }

        foreach (var (ent, _) in args.Ammo)
        {
            if (ent.HasValue && mech.EquipmentContainer.Contains(ent.Value))
            {
                _container.Remove(ent.Value, mech.EquipmentContainer);
                _throwing.TryThrow(ent.Value, _random.NextVector2(), _random.Next(5));
            }
        }
    }

    private void ChargeGunBattery(EntityUid uid, BatteryComponent component)
    {
        if (!TryComp<MechEquipmentComponent>(uid, out var mechEquipment) || !mechEquipment.EquipmentOwner.HasValue)
            return;

        if (!TryComp<MechComponent>(mechEquipment.EquipmentOwner.Value, out var mech))
            return;

        var maxCharge = component.MaxCharge;
        var currentCharge = component.CurrentCharge;

        var chargeDelta = maxCharge - currentCharge;

        if (chargeDelta <= 0 || mech.Energy - chargeDelta < 0)
            return;

        if (!_mech.TryChangeEnergy(mechEquipment.EquipmentOwner.Value, -chargeDelta, mech))
            return;

        _battery.SetCharge(uid, component.MaxCharge, component);
    }
}
