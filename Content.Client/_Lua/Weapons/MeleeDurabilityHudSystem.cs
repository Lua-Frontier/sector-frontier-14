// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Client.Items;
using Content.Client.Items.UI;
using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared._Lua.Weapons;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Client._Lua.Weapons;

public sealed class MeleeDurabilityHudSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MeleeDurabilityComponent, ComponentHandleState>(OnHandleState);
    }

    private void OnHandleState(EntityUid uid, MeleeDurabilityComponent component, ref ComponentHandleState args)
    {
        if (args.Current is MeleeDurabilityComponentState state)
            component.DestroyThreshold = state.DestroyThreshold;
    }

    private void OnItemStatusCollect(EntityUid uid, MeleeDurabilityComponent component, ItemStatusCollectMessage args)
    {
        args.Controls.Add(new MeleeDurabilityStatusControl(uid, _entityManager));
    }
}

public sealed class MeleeDurabilityStatusControl : PollingItemStatusControl<MeleeDurabilityStatusControl.Data>
{
    private readonly EntityUid _weapon;
    private readonly IEntityManager _entityManager;
    private readonly RichTextLabel _label;

    public MeleeDurabilityStatusControl(EntityUid weapon, IEntityManager entityManager)
    {
        _weapon = weapon;
        _entityManager = entityManager;
        _label = new RichTextLabel { StyleClasses = { StyleNano.StyleClassItemStatus } };
        AddChild(_label);
        UpdateDraw();
    }

    protected override Data PollData()
    {
        if (!_entityManager.TryGetComponent<DamageableComponent>(_weapon, out var damageable) ||
            !_entityManager.TryGetComponent<MeleeDurabilityComponent>(_weapon, out var dur))
            return default;

        return new Data(damageable.TotalDamage, FixedPoint2.New(dur.DestroyThreshold));
    }

    protected override void Update(in Data data)
    {
        var ratio = data.MaxDamage > FixedPoint2.Zero
            ? 1f - (data.CurrentDamage / data.MaxDamage).Float()
            : 1f;

        ratio = Math.Clamp(ratio, 0f, 1f);
        var percent = (int)(ratio * 100);
        var color = ratio > 0.6f ? "green" : ratio > 0.3f ? "yellow" : "darkorange";

        _label.SetMarkup(Loc.GetString("gun-durability-status", ("color", color), ("percent", percent)));
    }

    public record struct Data(FixedPoint2 CurrentDamage, FixedPoint2 MaxDamage) : IEquatable<Data>;
}
