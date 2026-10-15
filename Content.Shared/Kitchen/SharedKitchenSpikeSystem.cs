using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Hands;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Kitchen.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Throwing;
using Robust.Shared.Containers;
using Robust.Shared.Serialization;

namespace Content.Shared.Kitchen;

/// <summary>
/// Used to butcher some entities like monkeys.
/// </summary>
public abstract class SharedKitchenSpikeSystem : EntitySystem
{
    [Dependency] protected readonly SharedContainerSystem ContainerSystem = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KitchenSpikeComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<KitchenSpikeComponent, CanDropTargetEvent>(OnCanDrop);

        // Prevent the victim from doing anything while on the spike.
        SubscribeLocalEvent<KitchenSpikeHookedComponent, ChangeDirectionAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<KitchenSpikeHookedComponent, UseAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<KitchenSpikeHookedComponent, ThrowAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<KitchenSpikeHookedComponent, DropAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<KitchenSpikeHookedComponent, AttackAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<KitchenSpikeHookedComponent, PickupAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<KitchenSpikeHookedComponent, IsEquippingAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<KitchenSpikeHookedComponent, IsUnequippingAttemptEvent>(OnAttempt);

        // Container Jank
        SubscribeLocalEvent<KitchenSpikeHookedComponent, AccessibleOverrideEvent>(OnAccessibleOverride);
    }

    private void OnInit(Entity<KitchenSpikeComponent> ent, ref ComponentInit args)
    {
        ent.Comp.BodyContainer = ContainerSystem.EnsureContainer<ContainerSlot>(ent, ent.Comp.ContainerId);
        ent.Comp.BodyContainer.ShowContents = true; // Lua
    }

    private void OnCanDrop(Entity<KitchenSpikeComponent> ent, ref CanDropTargetEvent args)
    {
        if (args.Handled)
            return;

        args.CanDrop = HasComp<ButcherableComponent>(args.Dragged);
        args.Handled = true;
    }

    private static void OnAttempt(EntityUid uid, KitchenSpikeHookedComponent component, CancellableEntityEventArgs args)
    {
        args.Cancel();
    }

    private void OnAccessibleOverride(Entity<KitchenSpikeHookedComponent> ent, ref AccessibleOverrideEvent args)
    {
        // Check if the entity is the target to avoid giving the hooked entity access to everything.
        // If we already have access we don't need to run more code.
        if (args.Accessible || args.Target != ent.Owner)
            return;

        var xform = Transform(ent);
        if (!_interaction.IsAccessible(args.User, xform.ParentUid))
            return;

        args.Accessible = true;
        args.Handled = true;
    }
}

/// <summary>
///     DoAfter event for putting an entity on a meat spike.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class SpikeHookDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
///     DoAfter event for removing an entity from a meat spike.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class SpikeUnhookDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
///     DoAfter event for slicing an entity on a meat spike.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class SpikeButcherDoAfterEvent : SimpleDoAfterEvent;
