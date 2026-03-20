// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Lua.Weapons;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Robust.Shared.GameObjects;

namespace Content.Server._Lua.Weapons;

public sealed class WeaponDurabilityExamineSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MeleeDurabilityComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<GunJamComponent, ExaminedEvent>(OnExamineGun);
    }

    private void OnExamine(EntityUid uid, MeleeDurabilityComponent component, ExaminedEvent args)
    {
        if (!TryComp<DamageableComponent>(uid, out var damageable))
            return;

        ShowDurability(args, damageable.TotalDamage.Float(), component.DestroyThreshold);
    }

    private void OnExamineGun(EntityUid uid, GunJamComponent component, ExaminedEvent args)
    {
        if (!TryComp<DamageableComponent>(uid, out var damageable))
            return;

        ShowDurability(args, damageable.TotalDamage.Float(), component.DestroyThreshold);
    }

    private void ShowDurability(ExaminedEvent args, float currentDamage, float threshold)
    {
        if (threshold <= 0f)
            return;

        var ratio = Math.Clamp(1f - currentDamage / threshold, 0f, 1f);
        var percent = (int)(ratio * 100);

        const int barLength = 10;
        var filled = (int)Math.Round(ratio * barLength);
        var bar = new string('█', filled) + new string('░', barLength - filled);

        var color = ratio > 0.6f ? "green" : ratio > 0.3f ? "yellow" : "red";

        args.PushMarkup(Loc.GetString("weapon-examine-durability",
            ("color", color),
            ("bar", bar),
            ("percent", percent)), -10);
    }
}
