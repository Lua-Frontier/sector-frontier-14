// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server.Destructible;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Shared._Lua.Weapons;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Lua.Weapons;

public sealed class GunJamSystem : SharedGunJamSystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly DestructibleSystem _destructible = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunJamComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<GunJamComponent, AmmoShotEvent>(OnAmmoShot);
    }

    private void OnMapInit(Entity<GunJamComponent> ent, ref MapInitEvent args)
    {
        var threshold = _destructible.DestroyedAt(ent.Owner);
        if (threshold > FixedPoint2.Zero && threshold != FixedPoint2.MaxValue)
        {
            ent.Comp.DestroyThreshold = threshold.Float();
        }
        if (HasComp<BatterySelfRechargerComponent>(ent.Owner))
        {
            ent.Comp.IsEnergyWeapon = true;
        }

        Dirty(ent);
    }

    private void OnAmmoShot(Entity<GunJamComponent> ent, ref AmmoShotEvent args)
    {
        if (!ent.Comp.CanJam || args.FiredProjectiles.Count == 0)
            return;
        var damage = new DamageSpecifier();
        damage.DamageDict["Structural"] = FixedPoint2.New(ent.Comp.DamagePerShot * args.FiredProjectiles.Count);
        _damageable.TryChangeDamage(ent.Owner, damage, origin: ent.Owner);

        if (!TryComp<DamageableComponent>(ent, out var damageable))
            return;

        var destroyThreshold = _destructible.DestroyedAt(ent.Owner);
        if (destroyThreshold <= FixedPoint2.Zero || destroyThreshold == FixedPoint2.MaxValue)
            return;
        var damageRatio = (damageable.TotalDamage / destroyThreshold).Float();
        if (damageRatio < ent.Comp.JamThreshold) return;
        var scaled = (damageRatio - ent.Comp.JamThreshold) / (1f - ent.Comp.JamThreshold);
        var jamChance = scaled * scaled * ent.Comp.MaxJamChance;

        if (!_random.Prob(jamChance))
            return;

        var user = Transform(ent).ParentUid;
        var userOrNull = EntityManager.EntityExists(user) ? user : (EntityUid?) null;

        if (ent.Comp.IsEnergyWeapon)
        {
            ent.Comp.EnergyJamUntil = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.EnergyJamDuration);
            Dirty(ent);

            if (userOrNull != null)
                _popup.PopupEntity(Loc.GetString("gun-jam-energy-occurred"), ent.Owner, userOrNull.Value, PopupType.MediumCaution);
        }
        else if (TryComp<ChamberMagazineAmmoProviderComponent>(ent, out var chamber) && chamber.BoltClosed != null)
        {
            ent.Comp.IsJammed = true;
            Dirty(ent);
            _gun.SetBoltClosed(ent.Owner, chamber, false, userOrNull);
        }
    }
}
