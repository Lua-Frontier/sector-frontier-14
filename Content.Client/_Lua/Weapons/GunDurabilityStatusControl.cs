// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Client.Items.UI;
using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared._Lua.Weapons;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client._Lua.Weapons;

public sealed class GunDurabilityStatusControl : PollingItemStatusControl<GunDurabilityStatusControl.Data>
{
    private readonly EntityUid _gun;
    private readonly IEntityManager _entityManager;
    private readonly IGameTiming _timing;
    private readonly RichTextLabel _label;

    public GunDurabilityStatusControl(EntityUid gun, IEntityManager entityManager, IGameTiming timing)
    {
        _gun = gun;
        _entityManager = entityManager;
        _timing = timing;
        _label = new RichTextLabel { StyleClasses = { StyleNano.StyleClassItemStatus } };
        AddChild(_label);
        UpdateDraw();
    }

    protected override Data PollData()
    {
        if (!_entityManager.TryGetComponent<DamageableComponent>(_gun, out var damageable) ||
            !_entityManager.TryGetComponent<GunJamComponent>(_gun, out var jam))
            return default;

        var energyLocked = jam.IsEnergyWeapon && jam.EnergyJamUntil > _timing.CurTime;
        return new Data(damageable.TotalDamage, FixedPoint2.New(jam.DestroyThreshold), jam.IsJammed, energyLocked, jam.EnergyJamUntil);
    }

    protected override void Update(in Data data)
    {
        var ratio = data.MaxDamage > FixedPoint2.Zero
            ? 1f - (data.CurrentDamage / data.MaxDamage).Float()
            : 1f;

        ratio = Math.Clamp(ratio, 0f, 1f);
        var percent = (int)(ratio * 100);

        if (data.IsJammed)
        {
            _label.SetMarkup(Loc.GetString("gun-durability-status-jammed", ("percent", percent)));
            return;
        }

        if (data.EnergyLocked)
        {
            var remaining = (int)Math.Ceiling((data.EnergyJamUntil - _timing.CurTime).TotalSeconds);
            _label.SetMarkup(Loc.GetString("gun-durability-status-energy-jam", ("percent", percent), ("seconds", remaining)));
            return;
        }

        var color = ratio > 0.6f ? "green" : ratio > 0.3f ? "yellow" : "darkorange";
        _label.SetMarkup(Loc.GetString("gun-durability-status", ("color", color), ("percent", percent)));
    }

    public record struct Data(
        FixedPoint2 CurrentDamage,
        FixedPoint2 MaxDamage,
        bool IsJammed,
        bool EnergyLocked,
        TimeSpan EnergyJamUntil) : IEquatable<Data>;
}
