using Content.Shared.Actions;
using Content.Shared.Clothing.Components;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Strip;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Map.Events;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Shared.Clothing.EntitySystems;

// Goobstation - Modsuits fully change this system.
public sealed partial class ToggleableClothingSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _netMan = default!;
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private SharedActionsSystem _actionsSystem = default!;
    [Dependency] private ActionContainerSystem _actionContainer = default!;
    [Dependency] private InventorySystem _inventorySystem = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedStrippableSystem _strippable = default!;
    [Dependency] private SharedUserInterfaceSystem _uiSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ToggleableClothingComponent, ComponentInit>(OnToggleableInit);
        SubscribeLocalEvent<ToggleableClothingComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ToggleableClothingComponent, ToggleClothingEvent>(OnToggleClothingAction);
        SubscribeLocalEvent<ToggleableClothingComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<ToggleableClothingComponent, ComponentRemove>(OnRemoveToggleable);
        SubscribeLocalEvent<ToggleableClothingComponent, GotUnequippedEvent>(OnToggleableUnequip);
        SubscribeLocalEvent<ToggleableClothingComponent, ToggleableClothingUiMessage>(OnToggleClothingMessage);
        SubscribeLocalEvent<ToggleableClothingComponent, BeingUnequippedAttemptEvent>(OnToggleableUnequipAttempt);

        SubscribeLocalEvent<AttachedClothingComponent, ComponentInit>(OnAttachedInit);
        SubscribeLocalEvent<AttachedClothingComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<AttachedClothingComponent, GotUnequippedEvent>(OnAttachedUnequip);
        SubscribeLocalEvent<AttachedClothingComponent, ComponentRemove>(OnRemoveAttached);
        SubscribeLocalEvent<AttachedClothingComponent, BeingUnequippedAttemptEvent>(OnAttachedUnequipAttempt);

        SubscribeLocalEvent<ToggleableClothingComponent, InventoryRelayedEvent<GetVerbsEvent<EquipmentVerb>>>(GetRelayedVerbs);
        SubscribeLocalEvent<ToggleableClothingComponent, GetVerbsEvent<EquipmentVerb>>(OnGetVerbs);
        SubscribeLocalEvent<AttachedClothingComponent, GetVerbsEvent<EquipmentVerb>>(OnGetAttachedStripVerbsEvent);
        SubscribeLocalEvent<ToggleableClothingComponent, ToggleClothingDoAfterEvent>(OnDoAfterComplete);
        SubscribeLocalEvent<BeforeSerializationEvent>(OnBeforeSave);
    }

    private void OnBeforeSave(BeforeSerializationEvent ev)
    {
        var query = AllEntityQuery<ToggleableClothingComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var dirty = false;

            foreach (var clothing in comp.ClothingUids.Keys.ToArray())
            {
                if (TerminatingOrDeleted(clothing))
                {
                    comp.ClothingUids.Remove(clothing);
                    dirty = true;
                }
            }

            if (comp.ActionEntity != null && TerminatingOrDeleted(comp.ActionEntity.Value))
            {
                comp.ActionEntity = null;
                dirty = true;
            }

            if (dirty) Dirty(uid, comp);
        }
    }

    private void GetRelayedVerbs(Entity<ToggleableClothingComponent> toggleable, ref InventoryRelayedEvent<GetVerbsEvent<EquipmentVerb>> args)
    {
        OnGetVerbs(toggleable, ref args.Args);
    }

    private void OnGetVerbs(Entity<ToggleableClothingComponent> toggleable, ref GetVerbsEvent<EquipmentVerb> args)
    {
        var comp = toggleable.Comp;

        if (!args.CanAccess || !args.CanInteract || args.Hands == null || comp.ClothingUids.Count == 0 || comp.Container == null)
            return;

        var text = comp.VerbText ?? (comp.ActionEntity == null ? null : Name(comp.ActionEntity.Value));
        if (text == null)
            return;

        if (!_inventorySystem.InSlotWithFlags(toggleable.Owner, comp.RequiredFlags))
            return;

        var wearer = Transform(toggleable).ParentUid;
        if (args.User != wearer && comp.StripDelay == null)
            return;

        var user = args.User;

        var verb = new EquipmentVerb()
        {
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/outfit.svg.192dpi.png")),
            Text = Loc.GetString(text),
        };

        if (user == wearer)
        {
            verb.Act = () => ToggleClothing(user, toggleable);
        }
        else
        {
            verb.Act = () => StartDoAfter(user, toggleable, wearer);
        }

        args.Verbs.Add(verb);
    }

    private void StartDoAfter(EntityUid user, Entity<ToggleableClothingComponent> toggleable, EntityUid wearer)
    {
        var comp = toggleable.Comp;

        if (comp.StripDelay == null)
            return;

        var (time, stealth) = _strippable.GetStripTimeModifiers(user, wearer, toggleable, comp.StripDelay.Value);

        var args = new DoAfterArgs(EntityManager, user, time, new ToggleClothingDoAfterEvent(), toggleable, wearer, toggleable)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            DistanceThreshold = 2,
        };

        if (!_doAfter.TryStartDoAfter(args))
            return;

        if (!stealth)
        {
            var popup = Loc.GetString("strippable-component-alert-owner-interact", ("user", Identity.Entity(user, EntityManager)), ("item", toggleable));
            _popupSystem.PopupEntity(popup, wearer, wearer, PopupType.Large);
        }
    }

    private void OnGetAttachedStripVerbsEvent(Entity<AttachedClothingComponent> attached, ref GetVerbsEvent<EquipmentVerb> args)
    {
        var comp = attached.Comp;

        if (!TryComp<ToggleableClothingComponent>(comp.AttachedUid, out var toggleableComp))
            return;

        // redirect to the attached entity.
        OnGetVerbs((comp.AttachedUid, toggleableComp), ref args);
    }

    private void OnDoAfterComplete(Entity<ToggleableClothingComponent> toggleable, ref ToggleClothingDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        ToggleClothing(args.User, toggleable);
    }

    private void OnInteractHand(Entity<AttachedClothingComponent> attached, ref InteractHandEvent args)
    {
        var comp = attached.Comp;

        if (args.Handled)
            return;

        if (!TryComp(comp.AttachedUid, out ToggleableClothingComponent? toggleableComp)
            || toggleableComp.Container == null)
            return;

        if (!toggleableComp.ClothingUids.TryGetValue(attached.Owner, out var attachedSlot))
            return;

        if (!_inventorySystem.TryUnequip(Transform(attached.Owner).ParentUid, attachedSlot, force: true))
            return;

        _containerSystem.Insert(attached.Owner, toggleableComp.Container);
        args.Handled = true;
    }

    private void OnToggleableUnequipAttempt(Entity<ToggleableClothingComponent> toggleable, ref BeingUnequippedAttemptEvent args)
    {
        var comp = toggleable.Comp;

        if (!comp.BlockUnequipWhenAttached)
            return;

        if (GetAttachedToggleStatus(toggleable) == ToggleableClothingAttachedStatus.NoneToggled)
            return;

        _popupSystem.PopupClient(Loc.GetString("toggleable-clothing-remove-all-attached-first"), args.Unequipee, args.Unequipee);
        args.Cancel();
    }

    /// <summary>
    ///     Called when the suit is unequipped, to ensure that attached clothing also gets unequipped.
    /// </summary>
    private void OnToggleableUnequip(Entity<ToggleableClothingComponent> toggleable, ref GotUnequippedEvent args)
    {
        var comp = toggleable.Comp;

        if (_timing.ApplyingState)
            return;

        if (comp.Container == null || comp.ClothingUids.Count == 0)
            return;

        foreach (var part in comp.ClothingUids)
        {
            if (comp.Container.Contains(part.Key))
                continue;

            if (string.IsNullOrEmpty(part.Value))
                continue;

            _inventorySystem.TryUnequip(args.Equipee, part.Value, force: true, triggerHandContact: true);
        }
    }

    private void OnRemoveToggleable(Entity<ToggleableClothingComponent> toggleable, ref ComponentRemove args)
    {
        var comp = toggleable.Comp;

        _actionsSystem.RemoveAction(comp.ActionEntity);

        if (_netMan.IsClient)
            return;

        foreach (var clothing in comp.ClothingUids.Keys)
        {
            QueueDel(clothing);
        }
    }

    private void OnAttachedUnequipAttempt(Entity<AttachedClothingComponent> attached, ref BeingUnequippedAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnRemoveAttached(Entity<AttachedClothingComponent> attached, ref ComponentRemove args)
    {
        var comp = attached.Comp;

        if (!TryComp(comp.AttachedUid, out ToggleableClothingComponent? toggleableComp))
            return;

        if (toggleableComp.LifeStage > ComponentLifeStage.Running)
            return;

        var clothingUids = toggleableComp.ClothingUids;

        if (!clothingUids.Remove(attached.Owner))
            return;

        if (clothingUids.Count > 0)
            return;

        _actionsSystem.RemoveAction(toggleableComp.ActionEntity);
        RemComp(comp.AttachedUid, toggleableComp);
    }

    /// <summary>
    ///     Called if attached clothing was unequipped, to ensure that it gets moved into the suit's container.
    /// </summary>
    private void OnAttachedUnequip(Entity<AttachedClothingComponent> attached, ref GotUnequippedEvent args)
    {
        var comp = attached.Comp;

        if (_timing.ApplyingState)
            return;

        if (comp.LifeStage > ComponentLifeStage.Running)
            return;

        if (!TryComp(comp.AttachedUid, out ToggleableClothingComponent? toggleableComp))
            return;

        if (toggleableComp.LifeStage > ComponentLifeStage.Running)
            return;

        if (!toggleableComp.ClothingUids.ContainsKey(attached.Owner))
            return;

        if (toggleableComp.Container != null)
            _containerSystem.Insert(attached.Owner, toggleableComp.Container);
    }

    private void OnToggleClothingMessage(Entity<ToggleableClothingComponent> toggleable, ref ToggleableClothingUiMessage args)
    {
        var attachedUid = GetEntity(args.AttachedClothingUid);

        ToggleClothing(args.Actor, toggleable, attachedUid);
    }

    private void OnToggleClothingAction(Entity<ToggleableClothingComponent> toggleable, ref ToggleClothingEvent args)
    {
        var comp = toggleable.Comp;

        if (args.Handled)
            return;

        if (comp.Container == null || comp.ClothingUids.Count == 0)
            return;

        args.Handled = true;

        if (comp.ClothingUids.Count == 1)
            ToggleClothing(args.Performer, toggleable, comp.ClothingUids.First().Key);
        else if (comp.UseRadialMenu)
            _uiSystem.OpenUi(toggleable.Owner, ToggleClothingUiKey.Key, args.Performer);
        else
            ToggleClothing(args.Performer, toggleable);
    }

    private void ToggleClothing(EntityUid user, Entity<ToggleableClothingComponent> toggleable, EntityUid attachedUid)
    {
        var comp = toggleable.Comp;
        var attachedClothings = comp.ClothingUids;
        var container = comp.Container;

        if (!CanToggleClothing(user, toggleable))
            return;

        if (!attachedClothings.TryGetValue(attachedUid, out var slot) || string.IsNullOrEmpty(slot))
            return;

        if (!container!.Contains(attachedUid))
            UnequipClothing(user, toggleable, attachedUid, slot);
        else
            EquipClothing(user, toggleable, attachedUid, slot);
    }

    private void ToggleClothing(EntityUid user, Entity<ToggleableClothingComponent> toggleable)
    {
        var comp = toggleable.Comp;
        var attachedClothings = comp.ClothingUids;
        var container = comp.Container;

        if (!CanToggleClothing(user, toggleable))
            return;

        if (GetAttachedToggleStatus(toggleable, comp) == ToggleableClothingAttachedStatus.NoneToggled)
        {
            foreach (var clothing in attachedClothings)
            {
                EquipClothing(user, toggleable, clothing.Key, clothing.Value);
            }
        }
        else
        {
            foreach (var clothing in attachedClothings)
            {
                if (!container!.Contains(clothing.Key))
                    UnequipClothing(user, toggleable, clothing.Key, clothing.Value);
            }
        }
    }

    public void ToggleClothing(EntityUid user, EntityUid target, ToggleableClothingComponent component) // Frontier: public wrapper for AutoToggleableOuterClothingSystem.
    {
        ToggleClothing(user, (target, component));
    }

    private bool CanToggleClothing(EntityUid user, Entity<ToggleableClothingComponent> toggleable)
    {
        var comp = toggleable.Comp;
        var attachedClothings = comp.ClothingUids;
        var container = comp.Container;

        if (container == null || attachedClothings.Count == 0)
            return false;

        var ev = new ToggleClothingAttemptEvent(user, toggleable);
        RaiseLocalEvent(toggleable, ev);

        return !ev.Cancelled;
    }

    private void UnequipClothing(EntityUid user, Entity<ToggleableClothingComponent> toggleable, EntityUid clothing, string slot)
    {
        var parent = Transform(toggleable.Owner).ParentUid;

        _inventorySystem.TryUnequip(user, parent, slot, force: true, triggerHandContact: true);

        if (!TryComp<AttachedClothingComponent>(clothing, out var attachedComp) || attachedComp.ClothingContainer == null)
            return;

        var storedClothing = attachedComp.ClothingContainer.ContainedEntity;

        if (storedClothing != null)
            _inventorySystem.TryEquip(parent, storedClothing.Value, slot, force: true, triggerHandContact: true);
    }

    private void EquipClothing(EntityUid user, Entity<ToggleableClothingComponent> toggleable, EntityUid clothing, string slot)
    {
        var parent = Transform(toggleable.Owner).ParentUid;
        var comp = toggleable.Comp;

        if (_inventorySystem.TryGetSlotEntity(parent, slot, out var currentClothing))
        {
            if (!TryComp<AttachedClothingComponent>(clothing, out var attachedComp) || !comp.ReplaceCurrentClothing)
            {
                _popupSystem.PopupClient(Loc.GetString("toggleable-clothing-remove-first", ("entity", currentClothing)), user, user);
                return;
            }

            if (attachedComp.ClothingContainer == null || attachedComp.ClothingContainer.ContainedEntity != null)
                return;

            if (_inventorySystem.TryUnequip(user, parent, slot, triggerHandContact: true))
                _containerSystem.Insert(currentClothing.Value, attachedComp.ClothingContainer);
        }

        _inventorySystem.TryEquip(user, parent, clothing, slot, triggerHandContact: true);
    }

    private void OnGetActions(Entity<ToggleableClothingComponent> toggleable, ref GetItemActionsEvent args)
    {
        var comp = toggleable.Comp;

        if (comp.ClothingUids.Count == 0 || comp.ActionEntity == null || (args.SlotFlags & comp.RequiredFlags) != comp.RequiredFlags)
            return;

        args.AddAction(comp.ActionEntity.Value);
    }

    private void OnToggleableInit(Entity<ToggleableClothingComponent> toggleable, ref ComponentInit args)
    {
        var comp = toggleable.Comp;

        comp.Container = _containerSystem.EnsureContainer<Container>(toggleable, comp.ContainerId);
    }

    private void OnAttachedInit(Entity<AttachedClothingComponent> attached, ref ComponentInit args)
    {
        var comp = attached.Comp;

        comp.ClothingContainer = _containerSystem.EnsureContainer<ContainerSlot>(attached, comp.ClothingContainerId);
    }

    private void OnMapInit(Entity<ToggleableClothingComponent> toggleable, ref MapInitEvent args)
    {
        var comp = toggleable.Comp;

        if (comp.Container!.Count != 0)
        {
            DebugTools.Assert(comp.ClothingUids.Count != 0, "Unexpected entity present inside of a toggleable clothing container.");
            return;
        }

        if (comp.ClothingUids.Count != 0 && comp.ActionEntity != null)
            return;

        if (comp.ClothingPrototype != null)
        {
            var slot = string.IsNullOrEmpty(comp.Slot) ? "head" : comp.Slot;
            if (!comp.ClothingPrototypes.ContainsKey(slot))
                comp.ClothingPrototypes.Add(slot, comp.ClothingPrototype.Value);
        }

        var xform = Transform(toggleable.Owner);

        foreach (var prototype in comp.ClothingPrototypes)
        {
            var spawned = Spawn(prototype.Value, xform.Coordinates);
            var attachedClothing = EnsureComp<AttachedClothingComponent>(spawned);
            attachedClothing.AttachedUid = toggleable;
            EnsureComp<ContainerManagerComponent>(spawned);

            comp.ClothingUids.Add(spawned, prototype.Key);
            _containerSystem.Insert(spawned, comp.Container, containerXform: xform);

            Dirty(spawned, attachedClothing);
        }

        Dirty(toggleable, comp);

        if (_actionContainer.EnsureAction(toggleable, ref comp.ActionEntity, out var action, comp.Action) && comp.ClothingUids.Count > 0)
            _actionsSystem.SetEntityIcon((comp.ActionEntity.Value, action), comp.ClothingUids.Keys.First());
    }

    public ToggleableClothingAttachedStatus GetAttachedToggleStatus(EntityUid toggleable, ToggleableClothingComponent? component = null)
    {
        if (!Resolve(toggleable, ref component))
            return ToggleableClothingAttachedStatus.NoneToggled;

        var container = component.Container;
        var attachedClothings = component.ClothingUids;

        if (container == null || attachedClothings.Count == 0)
            return ToggleableClothingAttachedStatus.NoneToggled;

        var toggledCount = 0;

        foreach (var attached in attachedClothings)
        {
            if (container.Contains(attached.Key))
                continue;

            toggledCount++;
        }

        if (toggledCount == 0)
            return ToggleableClothingAttachedStatus.NoneToggled;

        if (toggledCount < attachedClothings.Count)
            return ToggleableClothingAttachedStatus.PartlyToggled;

        return ToggleableClothingAttachedStatus.AllToggled;
    }

    public List<EntityUid>? GetAttachedClothingsList(EntityUid toggleable, ToggleableClothingComponent? component = null)
    {
        if (!Resolve(toggleable, ref component) || component.ClothingUids.Count == 0)
            return null;

        var newList = new List<EntityUid>();

        foreach (var attachee in component.ClothingUids)
            newList.Add(attachee.Key);

        return newList;
    }

    public void ClearActionEntity(EntityUid uid, ToggleableClothingComponent comp)
    {
        comp.ActionEntity = null;
        Dirty(uid, comp);
    }
}

public sealed partial class ToggleClothingEvent : InstantActionEvent
{
}

[Serializable, NetSerializable]
public sealed partial class ToggleClothingDoAfterEvent : SimpleDoAfterEvent
{
}

public sealed class ToggleClothingAttemptEvent : CancellableEntityEventArgs
{
    public EntityUid User { get; }
    public EntityUid Target { get; }

    public ToggleClothingAttemptEvent(EntityUid user, EntityUid target)
    {
        User = user;
        Target = target;
    }
}

[Serializable, NetSerializable]
public enum ToggleableClothingAttachedStatus : byte
{
    NoneToggled,
    PartlyToggled,
    AllToggled
}
