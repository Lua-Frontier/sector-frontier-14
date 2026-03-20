// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server.Destructible;
using Content.Shared._Lua.Weapons;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Random;

namespace Content.Server._Lua.Weapons;

public sealed class MeleeDurabilitySystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly DestructibleSystem _destructible = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MeleeDurabilityComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MeleeDurabilityComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<MeleeDurabilityComponent, ComponentGetState>(OnGetState);
    }

    private void OnMapInit(Entity<MeleeDurabilityComponent> ent, ref MapInitEvent args)
    {
        var threshold = _destructible.DestroyedAt(ent.Owner);
        if (threshold > FixedPoint2.Zero && threshold != FixedPoint2.MaxValue)
        {
            ent.Comp.DestroyThreshold = threshold.Float();
            Dirty(ent);
        }
    }

    private void OnGetState(EntityUid uid, MeleeDurabilityComponent component, ref ComponentGetState args)
    {
        args.State = new MeleeDurabilityComponentState(component.DestroyThreshold);
    }

    private void OnMeleeHit(EntityUid uid, MeleeDurabilityComponent component, MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        if (!_random.Prob(component.DamageChance))
            return;

        var damage = new DamageSpecifier();
        damage.DamageDict["Structural"] = FixedPoint2.New(component.DamagePerHit * args.HitEntities.Count);
        _damageable.TryChangeDamage(uid, damage, origin: uid);
    }
}
