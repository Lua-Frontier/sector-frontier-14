// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Shared._Lua.Actions.Components;
using Content.Shared._Lua.Actions.Events;
using Content.Server.Actions;
using Content.Server.Popups;

namespace Content.Server._Lua.Actions.Systems;

public sealed class SoftNightVisionSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SoftNightVisionComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SoftNightVisionComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SoftNightVisionComponent, ToggleSoftNightVisionActionEvent>(OnToggle);
    }

    private void OnMapInit(EntityUid entity, SoftNightVisionComponent comp, MapInitEvent args)
    {
        if (comp.ActionEntity != null)
        {
            return;
        }

        _actions.AddAction(entity, ref comp.ActionEntity, comp.Action);
    }

    private void OnShutdown(EntityUid entity, SoftNightVisionComponent comp, ComponentShutdown args)
    {
        _actions.RemoveAction(comp.ActionEntity);
    }

    private void OnToggle(EntityUid entity, SoftNightVisionComponent comp, ToggleSoftNightVisionActionEvent args)
    {
        if (args.Handled)
        {
            return;
        }

        args.Handled = true;
        comp.Enable = !comp.Enable;
        var message = comp.Enable ? "soft-night-vision-enable" : "soft-night-vision-disable";
        _popup.PopupEntity(Loc.GetString(message), entity, entity);
        _actions.SetToggled(args.Action.AsNullable(), comp.Enable);
        Dirty(entity, comp);
    }
}
