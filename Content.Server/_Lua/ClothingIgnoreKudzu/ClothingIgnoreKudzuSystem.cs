using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Movement.Components;

namespace Content.Server.Movement.Systems;

public sealed class ClothingIgnoreKudzuSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClothingIgnoreKudzuComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<ClothingIgnoreKudzuComponent, GotUnequippedEvent>(OnUnequipped);
    }

    private void OnEquipped(EntityUid uid, ClothingIgnoreKudzuComponent component, GotEquippedEvent args)
    {
        if (args.Slot != "outerClothing" && args.Slot != "suit")
            return;

        EntityManager.AddComponent<IgnoreKudzuComponent>(args.Equipee);
    }

    private void OnUnequipped(EntityUid uid, ClothingIgnoreKudzuComponent component, GotUnequippedEvent args)
    {
        if (EntityManager.HasComponent<IgnoreKudzuComponent>(args.Equipee))
        {
            EntityManager.RemoveComponent<IgnoreKudzuComponent>(args.Equipee);
        }
    }
}