using Content.Shared.Gravity;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Alert;
using Content.Shared.Item;
using Content.Shared.Item.ItemToggle;
using Robust.Shared.Containers;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared._NF.Clothing.Components;
using Content.Shared.Clothing;
using Content.Shared.Standing; // Проверка состояния стоя/лежа

namespace Content.Shared._NF.Clothing.EntitySystems;

    public sealed class SharedNFMoonBootsSystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly ClothingSystem _clothing = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly ItemToggleSystem _toggle = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!; // Система стояния/лежания
        [Dependency] private readonly SharedGravitySystem _gravity = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NFMoonBootsComponent, ItemToggledEvent>(OnToggled);
        SubscribeLocalEvent<NFMoonBootsComponent, ClothingGotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<NFMoonBootsComponent, ClothingGotUnequippedEvent>(OnGotUnequipped);
        SubscribeLocalEvent<NFMoonBootsComponent, IsWeightlessEvent>(OnIsWeightless);
        SubscribeLocalEvent<NFMoonBootsComponent, InventoryRelayedEvent<IsWeightlessEvent>>(OnIsWeightless);
    }

    private void OnToggled(Entity<NFMoonBootsComponent> ent, ref ItemToggledEvent args)
    {
        var (uid, comp) = ent;
        // only works if being worn in the correct slot
        if (_container.TryGetContainingContainer((uid, null, null), out var container) &&
            _inventory.TryGetSlotEntity(container.Owner, comp.Slot, out var worn)
            && uid == worn)
        {
                UpdateMoonbootEffects(container.Owner, ent, args.Activated);

                // Если включают ботинки лежа — сразу поднимем персонажа
                if (args.Activated && HasComp<StandingStateComponent>(container.Owner) && _standing.IsDown(container.Owner))
                {
                    _standing.Stand(container.Owner, force: true);
                }
        }

        var prefix = args.Activated ? "on" : null;
        _item.SetHeldPrefix(ent, prefix);
        _clothing.SetEquippedPrefix(ent, prefix);
    }

    private void OnGotUnequipped(Entity<NFMoonBootsComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        UpdateMoonbootEffects(args.Wearer, ent, false);
    }

    private void OnGotEquipped(Entity<NFMoonBootsComponent> ent, ref ClothingGotEquippedEvent args)
    {
        UpdateMoonbootEffects(args.Wearer, ent, _toggle.IsActivated(ent.Owner));
    }

    public void UpdateMoonbootEffects(EntityUid user, Entity<NFMoonBootsComponent> ent, bool state)
    {
        if (state)
            _alerts.ShowAlert(user, ent.Comp.MoonBootsAlert);
        else
            _alerts.ClearAlert(user, ent.Comp.MoonBootsAlert);
    }

        private void OnIsWeightless(Entity<NFMoonBootsComponent> ent, ref IsWeightlessEvent args)
    {
            if (args.Handled)
                return;

            var wearer = args.Entity;

            // Если ботинки выключены и на этой сетке/карте есть гравитация — явно фиксируем, что невесомости нет,
            // чтобы избежать дерганья от конкурирующих обработчиков.
            if (!_toggle.IsActivated(ent.Owner))
            {
                if (_gravity.EntityGridOrMapHaveGravity((wearer, null)))
                {
                    args.IsWeightless = false;
                    args.Handled = true;
                }
                return;
            }

            // Если персонаж лежит — автоматически поднимаем перед включением невесомости
            if (HasComp<StandingStateComponent>(wearer) && _standing.IsDown(wearer))
            {
                _standing.Stand(wearer, force: true);
            }

        // Не завершаем обработку если уже выставлено состояние другим компонентом
        if (!args.Handled)
        {
            args.IsWeightless = true;
            args.Handled = true;
        }
    }

    private void OnIsWeightless(Entity<NFMoonBootsComponent> ent, ref InventoryRelayedEvent<IsWeightlessEvent> args)
    {
        OnIsWeightless(ent, ref args.Args);
    }
}
