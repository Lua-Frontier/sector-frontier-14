// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Map;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Content.Shared.Interaction;
using Content.Shared.Shuttles.Events;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Containers.ItemSlots;
using Content.Shared._NF.Shuttles.Events;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared._Lua.Shuttles.UI;
using Content.Shared._Lua.Shuttles.Components;
using Content.Shared.Shuttles.Components;
using Content.Shared._Mono.FireControl;
using Content.Server._Mono.FireControl;
using Content.Server.Shuttles.Components;
using Content.Server.Popups;
using Content.Server.PowerCell;
using Content.Server.Shuttles.Systems;
using Content.Server.Power.Components;
using Content.Server.Shuttles.Events;

namespace Content.Server._Lua.Shuttles.Systems;

public sealed class ShuttleTabletSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly PowerCellSystem _cell = default!;
    [Dependency] private readonly ItemSlotsSystem _slots = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly ShuttleConsoleSystem _shuttle = default!;
    [Dependency] private readonly FireControlSystem _fireControl = default!;

    private const string IDContainerSlot = "id_container";
    private const float LinkRangeDefault = 300f;
    private const float TabletUpdateTime = 2.0f;
    private float _tabletUpdateTimer;

    public override void Initialize()
    {
        base.Initialize();

        _tabletUpdateTimer = TabletUpdateTime;

        SubscribeLocalEvent<ShuttleTabletComponent, AfterInteractEvent>(OnConsoleLink);
        Subs.BuiEvents<ShuttleTabletComponent>(ShuttleTabletWindowUiKey.Key, subs =>
        {
            subs.Event<ShuttleConsoleFireMessage>(OnShuttleConsoleFire);
            subs.Event<ShuttleConsoleRefreshFireControlMessage>(OnShuttleConsoleRefreshFireControl);
            subs.Event<BoundUIClosedEvent>(OnUIClose);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _tabletUpdateTimer -= frameTime;

        if (_tabletUpdateTimer > 0f)
        {
            return;
        }

        var tabletQuery = EntityQueryEnumerator<ShuttleTabletComponent>();

        while (tabletQuery.MoveNext(out var tablet, out var tabletComp))
        {
            if (!_ui.IsUiOpen(tablet, ShuttleTabletWindowUiKey.Key))
            {
                continue;
            }

            UpdateTabletState(tablet, tabletComp);
        }

        _tabletUpdateTimer = TabletUpdateTime;
    }

    private void OnConsoleLink(EntityUid tablet, ShuttleTabletComponent tabletComp, AfterInteractEvent args)
    {
        if (args.Handled)
        {
            return;
        }

        var console = args.Target;

        if (!args.CanReach || !Exists(console))
        {
            return;
        }

        if (!HasComp<ShuttleConsoleComponent>(console))
        {
            return;
        }

        var isNewConsole = tabletComp.LinkedConsole != console;

        tabletComp.LinkedConsole = isNewConsole ? console : null;
        var linkedString = isNewConsole ? "shuttle-tablet-console-linked" : "shuttle-tablet-console-unlinked";

        _audio.PlayPvs(tabletComp.LinkSound, tablet);
        _popup.PopupEntity(Loc.GetString(linkedString), tablet);

        args.Handled = true;
    }

    private void OnUIClose(EntityUid uid, ShuttleTabletComponent component, BoundUIClosedEvent args)
    {
        if ((ShuttleTabletWindowUiKey)args.UiKey != ShuttleTabletWindowUiKey.Key)
        {
            return;
        }

        _shuttle.RemovePilot(args.Actor);
    }

    private void OnShuttleConsoleFire(EntityUid tablet, ShuttleTabletComponent tabletComp, ShuttleConsoleFireMessage args)
    {
        if (!tabletComp.CombatTablet)
        {
            return;
        }

        var grid = GetTabletGrid(tablet);

        if (grid == null
            || !TryComp<FireControlGridComponent>(grid, out var fcGrid)
            || !Exists(fcGrid.ControllingServer)
            || !TryComp<FireControlServerComponent>(fcGrid.ControllingServer, out var server))
        {
            return;
        }

        _fireControl.FireWeapons(fcGrid.ControllingServer.Value, args.Selected, args.Coordinates, server);
        var fireEvent = new FireControlConsoleFireEvent(args.Coordinates, args.Selected);
        RaiseLocalEvent(tablet, fireEvent);
    }

    private void OnShuttleConsoleRefreshFireControl(EntityUid tablet, ShuttleTabletComponent tabletComp, ShuttleConsoleRefreshFireControlMessage args)
    {
        if (!tabletComp.CombatTablet)
        {
            return;
        }

        var grid = GetTabletGrid(tablet);

        if (grid != null
            && HasComp<FireControlGridComponent>(grid))
        {
            _fireControl.RefreshControllables(grid.Value);
        }

        UpdateTabletState(tablet, tabletComp);
    }

    public void UpdateTabletState(EntityUid tablet, ShuttleTabletComponent tabletComp, DockingInterfaceState? dockState = null)
    {
        if (!_ui.HasUi(tablet, ShuttleTabletWindowUiKey.Key))
        {
            return;
        }

        if (!IsValidTablet(tablet, tabletComp, out var tabletLinkPower))
        {
            _ui.CloseUi(tablet, ShuttleTabletWindowUiKey.Key);
            return;
        }

        var getShuttleEv = new ConsoleShuttleEvent
        {
            Console = tablet
        };

        RaiseLocalEvent(tablet, ref getShuttleEv);
        var proxyTablet = getShuttleEv.Console;
        var shuttle = GetTabletGrid(proxyTablet);
        var shuttleExists = Exists(shuttle);

        dockState ??= _shuttle.GetDockState();

        var navState = (shuttleExists && proxyTablet != null)
        ? _shuttle.GetNavState(proxyTablet.Value, dockState.Docks)
        : new NavInterfaceState(0f, null, null, [], InertiaDampeningMode.Dampen, ServiceFlags.None, null, NetEntity.Invalid, true);

        var combatTablet = tabletComp.CombatTablet;
        var fcConnected = false;
        FireControllableEntry[]? fcControllables = null;

        if (combatTablet
            && shuttleExists
            && TryComp<FireControlGridComponent>(shuttle, out var fcGrid)
            && Exists(fcGrid.ControllingServer)
            && TryComp<FireControlServerComponent>(fcGrid.ControllingServer, out var fcServer))
        {
            fcConnected = true;
            List<FireControllableEntry> fcControllablesList = new();

            foreach (var c in fcServer.Controlled)
            {
                var controllableEntry = new FireControllableEntry(GetNetEntity(c), GetNetCoordinates(Transform(c).Coordinates), MetaData(c).EntityName);
                fcControllablesList.Add(controllableEntry);
            }

            fcControllables = [.. fcControllablesList];
        }

        var tabletState = new ShuttleTabletWindowInterfaceState(navState, dockState, GetNetEntity(shuttle), tabletLinkPower, combatTablet, fcConnected, fcControllables);
        _ui.SetUiState(tablet, ShuttleTabletWindowUiKey.Key, tabletState);
    }

    public bool IsValidTablet(EntityUid tablet, ShuttleTabletComponent tabletComp, out float linkPower)
    {
        linkPower = 0;

        if (!_cell.HasActivatableCharge(tablet)
            || !_cell.HasDrawCharge(tablet))
        {
            return false;
        }

        var card = _slots.GetItemOrNull(tablet, IDContainerSlot);

        if (card == null)
        {
            _popup.PopupEntity(Loc.GetString("shuttle-tablet-no-id"), tablet);
            return false;
        }

        if (!HasComp<ShuttleDeedComponent>(card))
        {
            _popup.PopupEntity(Loc.GetString("shuttle-tablet-no-deed"), tablet);
            return false;
        }

        var linkedConsole = tabletComp.LinkedConsole;

        if (!Exists(linkedConsole))
        {
            _popup.PopupEntity(Loc.GetString("shuttle-tablet-no-linked-console"), tablet);
            return false;
        }

        var consoleTransform = Transform(linkedConsole.Value);

        if (TryComp<ApcPowerReceiverComponent>(linkedConsole, out var consolePower)
            && !consolePower.Powered)
        {
            _popup.PopupEntity(Loc.GetString("shuttle-tablet-console-not-powered"), tablet);
            return false;
        }

        if (TryComp<ShuttleConsoleLockComponent>(linkedConsole, out var consoleLock)
            && consoleLock.EmergencyLocked)
        {
            _popup.PopupEntity(Loc.GetString("shuttle-tablet-console-emergency-locked"), tablet);
            return false;
        }

        if (consoleTransform.GridUid != GetTabletGrid(tablet))
        {
            _popup.PopupEntity(Loc.GetString("shuttle-tablet-wrong-console"), tablet);
            return false;
        }

        var tabletTransform = Transform(tablet);

        if (!tabletComp.IgnoreSector
            && consoleTransform.MapID != tabletTransform.MapID)
        {
            _popup.PopupEntity(Loc.GetString("shuttle-tablet-different-sectors"), tablet);
            return false;
        }

        var consoleWorld = _transform.GetWorldPosition(consoleTransform);
        var tabletWorld = _transform.GetWorldPosition(tabletTransform);
        var distance = (consoleWorld - tabletWorld).Length();
        var linkRange = tabletComp.LinkRange > 0 ? tabletComp.LinkRange : LinkRangeDefault;

        if (distance > linkRange)
        {
            _popup.PopupEntity(Loc.GetString("shuttle-tablet-out-of-range"), tablet);
            return false;
        }

        linkPower = (linkRange - distance) / linkRange;
        return true;
    }

    public EntityCoordinates? GetTabletCoordinates(EntityUid tablet)
    {
        if (!TryComp<ShuttleTabletComponent>(tablet, out var tabletComp))
        {
            return null;
        }

        var console = tabletComp.LinkedConsole;

        if (console == null)
        {
            return null;
        }

        return Transform(console.Value).Coordinates;
    }

    public EntityUid? GetTabletGrid(EntityUid? tablet)
    {
        if (tablet == null || !HasComp<ShuttleTabletComponent>(tablet))
        {
            return null;
        }

        var card = _slots.GetItemOrNull(tablet.Value, IDContainerSlot);

        if (card == null
            || !TryComp<ShuttleDeedComponent>(card, out var deedComp))
        {
            return null;
        }

        var shuttle = deedComp.ShuttleUid;

        if (shuttle == null || shuttle == EntityUid.Invalid)
        {
            return null;
        }

        return shuttle;
    }
}
