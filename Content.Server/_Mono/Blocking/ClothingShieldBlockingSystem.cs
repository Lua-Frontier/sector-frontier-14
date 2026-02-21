using Content.Shared.Blocking.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory;
using Content.Shared._Mono.Blocking.Components;

namespace Content.Server._Mono.Blocking;

public sealed class ClothingShieldBlockingSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BlockingComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnWearerDamageModifyRelayed);
    }

    private void OnWearerDamageModifyRelayed(EntityUid uid, BlockingComponent component, InventoryRelayedEvent<DamageModifyEvent> args)
    {
        if (!component.IsClothing) return;
        if (!TryComp<ShieldToggleComponent>(uid, out var shieldToggle) || !shieldToggle.Enabled) return;
        if (!args.Args.Damage.AnyPositive()) return;
        if (!TryComp<DamageableComponent>(uid, out _)) return;
        var fraction = component.IsBlocking ? component.ActiveBlockFraction : component.PassiveBlockFraction;
        fraction = Math.Clamp(fraction, 0f, 1f);
        if (fraction <= 0f) return;
        var shieldDamage = fraction * args.Args.OriginalDamage;
        _damageable.TryChangeDamage(uid, shieldDamage, origin: args.Args.Origin);
        var wearerMult = 1f - fraction;
        var reduce = new DamageModifierSet();
        foreach (var key in args.Args.Damage.DamageDict.Keys)
        { reduce.Coefficients.TryAdd(key, wearerMult); }
        args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, reduce);
    }
}

