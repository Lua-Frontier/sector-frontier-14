using Content.Server.Popups;
using Content.Shared._Mono.Blocking.Components;
using Content.Shared.Actions;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;

namespace Content.Server._Mono.Blocking;

public sealed class ShieldToggleSystem : EntitySystem
{
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShieldToggleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ShieldToggleComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<ShieldToggleComponent, ToggleShieldEvent>(OnToggle);
        SubscribeLocalEvent<ShieldToggleComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<ShieldToggleComponent, GotUnequippedEvent>(OnUnequipped);
    }

    private void OnMapInit(Entity<ShieldToggleComponent> ent, ref MapInitEvent args)
    {
        _actionContainer.EnsureAction(ent.Owner, ref ent.Comp.ToggleActionEntity, ent.Comp.ToggleAction);
        Dirty(ent);
    }

    private void OnGetActions(Entity<ShieldToggleComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.SlotFlags is not { } flags || (((flags & SlotFlags.OUTERCLOTHING) == 0) && ((flags & SlotFlags.BELT) == 0))) //Lua BELT slot added
            return;
        args.AddAction(ref ent.Comp.ToggleActionEntity, ent.Comp.ToggleAction);
    }

    private void OnEquipped(Entity<ShieldToggleComponent> ent, ref GotEquippedEvent args)
    {
        _actionContainer.EnsureAction(ent.Owner, ref ent.Comp.ToggleActionEntity, ent.Comp.ToggleAction);
        ent.Comp.Wearer = args.Equipee;
        Dirty(ent);
    }

    private void OnUnequipped(Entity<ShieldToggleComponent> ent, ref GotUnequippedEvent args)
    {
        SetEnabled(ent.Owner, ent.Comp, false);

        if (ent.Comp.ToggleActionEntity != null)
        {
            QueueDel(ent.Comp.ToggleActionEntity.Value);
            ent.Comp.ToggleActionEntity = null;
        }

        ent.Comp.Wearer = null;
        Dirty(ent);
    }

    private void OnToggle(Entity<ShieldToggleComponent> ent, ref ToggleShieldEvent args)
    {
        if (!ent.Comp.Enabled)
        {
            var attemptEv = new ShieldToggleAttemptEvent(args.Performer);
            RaiseLocalEvent(ent.Owner, ref attemptEv);

            if (attemptEv.Cancelled)
            {
                if (ent.Comp.SoundFailToActivate != null)
                    _audio.PlayPvs(ent.Comp.SoundFailToActivate, ent.Owner);
                args.Handled = true;
                return;
            }
        }

        SetEnabled(ent.Owner, ent.Comp, !ent.Comp.Enabled);

        var msg = ent.Comp.Enabled
            ? Loc.GetString("shield-toggle-on")
            : Loc.GetString("shield-toggle-off");
        _popup.PopupEntity(msg, args.Performer, args.Performer);

        args.Handled = true;
    }

    public void SetEnabled(EntityUid uid, ShieldToggleComponent? comp, bool enabled)
    {
        if (!Resolve(uid, ref comp) || comp.Enabled == enabled)
            return;

        comp.Enabled = enabled;
        Dirty(uid, comp);

        _pointLight.SetEnabled(uid, enabled);

        if (enabled)
        {
            if (comp.SoundActivate != null)
                _audio.PlayPvs(comp.SoundActivate, uid);

            if (comp.ActiveSound != null)
            {
                var loop = comp.ActiveSound.Params.WithLoop(true);
                var stream = _audio.PlayPvs(comp.ActiveSound, uid, loop);
                if (stream?.Entity is { } entity)
                    comp.PlayingStream = entity;
            }
        }
        else
        {
            if (comp.SoundDeactivate != null)
                _audio.PlayPvs(comp.SoundDeactivate, uid);

            comp.PlayingStream = _audio.Stop(comp.PlayingStream);
        }

        _actions.SetToggled(comp.ToggleActionEntity, enabled);

        if (comp.Wearer != null)
        {
            if (enabled)
            {
                var visuals = EnsureComp<BlockingVisualsComponent>(comp.Wearer.Value);
                visuals.Enabled = true;
                Dirty(comp.Wearer.Value, visuals);
            }
            else if (TryComp<BlockingVisualsComponent>(comp.Wearer.Value, out var visuals))
            {
                visuals.Enabled = false;
                Dirty(comp.Wearer.Value, visuals);
            }
        }
    }
}
