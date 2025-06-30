// SPDX-FileCopyrightText: 2025 Ark
// SPDX-FileCopyrightText: 2025 ark1368
// SPDX-FileCopyrightText: 2025 sleepyyapril
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// Copyright Rane (elijahrane@gmail.com) 2025
// All rights reserved. Relicensed under AGPL with permission

using Content.Server._Mono.Ships.Systems;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._Mono.FireControl;
using Content.Shared._Mono.Ships.Components;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;

namespace Content.Server._Mono.FireControl;

public sealed partial class FireControlSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ShuttleConsoleSystem _shuttleConsoleSystem = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly CrewedShuttleSystem _crewedShuttle = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private void InitializeConsole()
    {
        SubscribeLocalEvent<FireControlConsoleComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<FireControlConsoleComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<FireControlConsoleComponent, FireControlConsoleRefreshServerMessage>(OnRefreshServer);
        SubscribeLocalEvent<FireControlConsoleComponent, FireControlConsoleFireMessage>(OnFire);
        SubscribeLocalEvent<FireControlConsoleComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<FireControlConsoleComponent, ActivatableUIOpenAttemptEvent>(OnConsoleUIOpenAttempt);
    }

    private void OnPowerChanged(EntityUid uid, FireControlConsoleComponent component, PowerChangedEvent args)
    {
        if (args.Powered)
            TryRegisterConsole(uid, component);
        else
            UnregisterConsole(uid, component);
    }

    private void OnComponentShutdown(EntityUid uid, FireControlConsoleComponent component, ComponentShutdown args)
    {
        UnregisterConsole(uid, component);
    }

    private void OnRefreshServer(EntityUid uid, FireControlConsoleComponent component, FireControlConsoleRefreshServerMessage args)
    {
        if (component.ConnectedServer == null)
        {
            TryRegisterConsole(uid, component);
        }

        if (component.ConnectedServer != null &&
            TryComp<FireControlServerComponent>(component.ConnectedServer, out var server) &&
            server.ConnectedGrid != null)
        {
            RefreshControllables((EntityUid)server.ConnectedGrid);
        }
    }

    private void OnFire(EntityUid uid, FireControlConsoleComponent component, FireControlConsoleFireMessage args)
    {
        if (component.ConnectedServer == null || !TryComp<FireControlServerComponent>(component.ConnectedServer, out var server))
            return;

        // Fire the actual weapons
        FireWeapons((EntityUid)component.ConnectedServer, args.Selected, args.Coordinates, server);

        // Raise an event to track the cursor position even when not firing
        var fireEvent = new FireControlConsoleFireEvent(args.Coordinates, args.Selected);
        RaiseLocalEvent(uid, fireEvent);
    }

    public void OnUIOpened(EntityUid uid, FireControlConsoleComponent component, BoundUIOpenedEvent args)
    {
        UpdateUi(uid, component);
    }

    private void OnConsoleUIOpenAttempt(
        EntityUid uid,
        FireControlConsoleComponent component,
        ActivatableUIOpenAttemptEvent args)
    {
        var grid = Transform(uid).GridUid;
        var uiOpen = grid != null && _crewedShuttle.AnyShuttleConsoleActiveOnGridByPlayer(grid.Value, args.User);

        if (uiOpen)
        {
            args.Cancel();
            _popup.PopupEntity(Loc.GetString("shuttle-console-crewed"), uid, args.User);
        }
    }

    private void UnregisterConsole(EntityUid console, FireControlConsoleComponent? component = null)
    {
        if (!Resolve(console, ref component))
            return;

        if (component.ConnectedServer == null || !TryComp<FireControlServerComponent>(component.ConnectedServer, out var server))
            return;

        server.Consoles.Remove(console);
        component.ConnectedServer = null;
        UpdateUi(console, component);
    }
    private bool TryRegisterConsole(EntityUid console, FireControlConsoleComponent? consoleComponent = null)
    {
        if (!Resolve(console, ref consoleComponent))
            return false;

        var gridServer = TryGetGridServer(console);

        if (gridServer.ServerComponent == null)
            return false;

        if (gridServer.ServerComponent.Consoles.Add(console))
        {
            consoleComponent.ConnectedServer = gridServer.ServerUid;
            UpdateUi(console, consoleComponent);
            return true;
        }
        else
        {
            return false;
        }
    }

    private void UpdateUi(EntityUid uid, FireControlConsoleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        NavInterfaceState navState = _shuttleConsoleSystem.GetNavState(uid, _shuttleConsoleSystem.GetAllDocks());

        List<FireControllableEntry> controllables = new();
        if (component.ConnectedServer != null && TryComp<FireControlServerComponent>(component.ConnectedServer, out var server))
        {
            foreach (var controllable in server.Controlled)
            {
                var controlled = new FireControllableEntry();
                controlled.NetEntity = EntityManager.GetNetEntity(controllable);
                controlled.Coordinates = GetNetCoordinates(Transform(controllable).Coordinates);
                controlled.Name = MetaData(controllable).EntityName;

                controllables.Add(controlled);
            }
        }

        var array = controllables.ToArray();

        var state = new FireControlConsoleBoundInterfaceState(component.ConnectedServer != null, array, navState);
        _ui.SetUiState(uid, FireControlConsoleUiKey.Key, state);
    }
}
