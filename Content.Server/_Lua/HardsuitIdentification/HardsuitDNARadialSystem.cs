// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.
// Special Permission:
// In addition to AGPLv3, the author grants the "Мёртвый Космос" project
// the right to use this code under a separate custom license agreement.

using Content.Shared._Lua.HardsuitIdentification;
using Content.Shared.Actions;
using Content.Shared.Forensics.Components;
using Content.Shared.Inventory.Events;
using Robust.Shared.Player;

namespace Content.Server._Lua.HardsuitIdentification;

public sealed class HardsuitDNARadialSystem : EntitySystem
{
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HardsuitDNARadialComponent, GetItemActionsEvent>(OnGetItemActions);
        SubscribeLocalEvent<HardsuitDNARadialComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<HardsuitDNARadialComponent, OpenDNARadialActionEvent>(OnOpenDNARadial);
        SubscribeLocalEvent<HardsuitDNARadialComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<HardsuitDNARadialComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeNetworkEvent<SelectDNAActionEvent>(OnSelectDNAAction);
    }

    private void OnGetItemActions(EntityUid uid, HardsuitDNARadialComponent component, GetItemActionsEvent args)
    {
        _actionContainer.EnsureAction(uid, ref component.OpenDNARadialActionEntity, component.OpenDNARadialAction);
        args.AddAction(ref component.OpenDNARadialActionEntity, component.OpenDNARadialAction);
    }

    private void OnShutdown(EntityUid uid, HardsuitDNARadialComponent component, ComponentShutdown args)
    {
        if (component.OpenDNARadialActionEntity != null)
        {
            var action = component.OpenDNARadialActionEntity.Value;
            if (!TerminatingOrDeleted(action))
                _actionContainer.RemoveAction(action);
            component.OpenDNARadialActionEntity = null;
        }
    }

    private void OnEquipped(EntityUid uid, HardsuitDNARadialComponent component, GotEquippedEvent args)
    {
    }

    private void OnUnequipped(EntityUid uid, HardsuitDNARadialComponent component, GotUnequippedEvent args)
    {
        if (component.OpenDNARadialActionEntity != null)
        {
            var action = component.OpenDNARadialActionEntity.Value;
            if (!TerminatingOrDeleted(action))
                _actionContainer.RemoveAction(action);
            component.OpenDNARadialActionEntity = null;
        }
    }

    private void OnOpenDNARadial(EntityUid uid, HardsuitDNARadialComponent component, OpenDNARadialActionEvent args)
    {
        if (args.Handled)
            return;
        if (!TryComp<HardsuitIdentificationComponent>(uid, out var identification))
            return;
        var availableActionPrototypes = new List<string>();
        switch (identification.IdentificationMode)
        {
            case HardsuitIdentificationMode.Registration:
                availableActionPrototypes.Add("ActionHardsuitSaveDNA");
                if (identification.DNAWasStored && TryComp<DnaComponent>(args.Performer, out var userDna))
                {
                    bool isAuthorized = identification.AllowMultipleDNA
                        ? identification.AuthorizedDNA.Contains(userDna.DNA ?? string.Empty)
                        : userDna.DNA == identification.DNA;
                    if (isAuthorized)
                    {
                        availableActionPrototypes.Add("ActionHardsuitLockDNA");
                        availableActionPrototypes.Add("ActionHardsuitClearDNA");
                    }
                }
                break;
            case HardsuitIdentificationMode.Clearance:
            case HardsuitIdentificationMode.Locked:
                if (TryComp<DnaComponent>(args.Performer, out var userDna2))
                {
                    bool isAuthorized = identification.AllowMultipleDNA
                        ? identification.AuthorizedDNA.Contains(userDna2.DNA ?? string.Empty)
                        : userDna2.DNA == identification.DNA;
                    if (isAuthorized)
                    {
                        availableActionPrototypes.Add("ActionHardsuitClearDNA");
                    }
                }
                break;
        }
        if (TryComp<ActorComponent>(args.Performer, out var actor))
        {
            var ev = new RequestDNARadialMenuEvent(GetNetEntity(uid), availableActionPrototypes);
            RaiseNetworkEvent(ev, actor.PlayerSession);
        }
        args.Handled = true;
    }

    private void OnSelectDNAAction(SelectDNAActionEvent msg)
    {
        var target = GetEntity(msg.Target);
        var performer = GetEntity(msg.Performer);
        if (!TryComp<HardsuitIdentificationComponent>(target, out var identification))
            return;
        var identificationSystem = EntityManager.System<HardsuitIdentificationSystem>();
        switch (msg.ActionType)
        {
            case "ActionHardsuitSaveDNA":
                var storeEvent = new StoreDNAActionEvent { Performer = performer };
                identificationSystem.OnDNAStore(target, identification, storeEvent);
                break;
            case "ActionHardsuitClearDNA":
                var clearEvent = new ClearDNAActionEvent { Performer = performer };
                identificationSystem.OnDNAClear(target, identification, clearEvent);
                break;
            case "ActionHardsuitLockDNA":
                var lockEvent = new LockDNAActionEvent { Performer = performer };
                identificationSystem.OnDNALock(target, identification, lockEvent);
                break;
        }
    }
}
