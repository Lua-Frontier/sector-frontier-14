// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.
// Special Permission:
// In addition to AGPLv3, the author grants the "Мёртвый Космос" project
// the right to use this code under a separate custom license agreement.

using Content.Client.UserInterface.Controls;
using Content.Shared._Lua.HardsuitIdentification;
using Content.Shared.Actions.Components;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._Lua.HardsuitIdentification;

public sealed class HardsuitDNARadialSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerMan = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private SimpleRadialMenu? _openedMenu;
    private NetEntity _currentTarget = NetEntity.Invalid;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeNetworkEvent<RequestDNARadialMenuEvent>(HandleDNARadialMenuEvent);
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        _openedMenu?.Dispose();
    }

    private void OnPlayerDetached(PlayerDetachedEvent args)
    {
        _openedMenu?.Dispose();
    }

    private void HandleDNARadialMenuEvent(RequestDNARadialMenuEvent args)
    {
        if (_openedMenu != null)
            _openedMenu.Dispose();
        _currentTarget = args.Target;
        var options = new List<RadialMenuOption>();
        foreach (var actionPrototypeId in args.AvailableActionPrototypes)
        {
            if (!_prototypeManager.TryIndex<EntityPrototype>(actionPrototypeId, out var actionPrototype))
                continue;
            if (!actionPrototype.TryGetComponent<ActionComponent>(out var actionComp))
                continue;
            var sprite = actionComp.Icon;
            var option = new RadialMenuActionOption<string>(HandleActionClick, actionPrototypeId)
            {
                Sprite = sprite,
                ToolTip = actionPrototype.Name
            };
            options.Add(option);
        }
        if (options.Count == 0)
            return;
        _openedMenu = new SimpleRadialMenu();
        _openedMenu.SetButtons(options);
        _openedMenu.OpenOverMouseScreenPosition();
        _openedMenu.OnClose += () =>
        {
            _openedMenu = null;
            _currentTarget = NetEntity.Invalid;
        };
    }

    private void HandleActionClick(string actionPrototypeId)
    {
        if (_openedMenu == null)
            return;
        var performer = _playerMan.LocalEntity ?? EntityUid.Invalid;
        var ev = new SelectDNAActionEvent(_currentTarget, GetNetEntity(performer), actionPrototypeId);
        RaiseNetworkEvent(ev);
        _openedMenu.Dispose();
    }
}
