// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server.Shuttles.Systems;
using Content.Lua.Shared.Shipyard.BUI;
using Content.Lua.Shared.Shipyard.BUIStates;
using Content.Lua.Shared.Shipyard.Components;
using Content.Lua.Shared.Shipyard.Events;
using Content.Shared._NF.Shipyard;
using Content.Shared._NF.Shipyard.BUI;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared._NF.Shuttles.Events;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._NF.Shipyard.Systems;

public sealed partial class ShipyardSystem
{
    [Dependency] private readonly ShuttleConsoleSystem _shuttleConsole = default!;
    private void InitializeDockSelect()
    {
        SubscribeLocalEvent<ShipyardConsoleComponent, SelectDockPortMessage>(OnSelectDockPort);
    }

    private void OnSelectDockPort(EntityUid uid, ShipyardConsoleComponent component, SelectDockPortMessage args)
    {
        var lua = EnsureComp<ShipyardLuaConsoleComponent>(uid);
        lua.SelectedDockPort = args.SelectedDockPort;
        Dirty(uid, lua);
        var uiKey = lua.ParkingConsole ? ShipyardConsoleUiKey.Parking : ShipyardConsoleUiKey.Shipyard;
        if (_ui.TryGetUiState<BoundUserInterfaceState>(uid, uiKey, out var currentState))
        {
            switch (currentState)
            {
                case ShipyardConsoleLuaDockSelectState dockLua:
                    RefreshState(uid, dockLua.BaseState.Balance, dockLua.BaseState.AccessGranted, dockLua.BaseState.ShipDeedTitle, dockLua.BaseState.ShipSellValue, component.TargetIdSlot.ContainerSlot?.ContainedEntity, uiKey, dockLua.BaseState.FreeListings);
                    break;
                case ShipyardConsoleInterfaceState baseOnly:
                    RefreshState(uid, baseOnly.Balance, baseOnly.AccessGranted, baseOnly.ShipDeedTitle, baseOnly.ShipSellValue, component.TargetIdSlot.ContainerSlot?.ContainedEntity, uiKey, baseOnly.FreeListings);
                    break;
                case ParkingConsoleLuaDockSelectState parkingLua:
                    RefreshParkingState(uid, parkingLua.BaseState.ShipDeedTitle, component.TargetIdSlot.ContainerSlot?.ContainedEntity);
                    break;
                case ParkingConsoleInterfaceState parkingBase:
                    RefreshParkingState(uid, parkingBase.ShipDeedTitle, component.TargetIdSlot.ContainerSlot?.ContainedEntity);
                    break;
            }
        }
    }

    partial void ExtendUiStateLua(EntityUid uid, ref BoundUserInterfaceState state)
    {
        if (!TryComp<ShipyardConsoleComponent>(uid, out _))
            return;

        TryComp(uid, out ShipyardLuaConsoleComponent? lua);
        var xform = Transform(uid);
        if (xform.GridUid is not { Valid: true } gridUid)
            return;

        var allDocks = _shuttleConsole.GetAllDocks();
        var gridNet = GetNetEntity(gridUid);
        var dockDict = new Dictionary<NetEntity, List<DockingPortState>>();
        if (allDocks.TryGetValue(gridNet, out var ports))
            dockDict[gridNet] = ports;

        var centerEntity = xform.ParentUid != EntityUid.Invalid ? xform.ParentUid : uid;
        var netCoords = new NetCoordinates(GetNetEntity(centerEntity), xform.LocalPosition);
        var angle = Angle.Zero;
        var gridComp = Comp<MapGridComponent>(gridUid);
        var w = gridComp.LocalAABB.Width;
        var h = gridComp.LocalAABB.Height;
        var radius = MathF.Sqrt(w * w + h * h) * 0.5f + 5f;
        var nav = new NavInterfaceState(radius, netCoords, angle, dockDict, InertiaDampeningMode.Dampen, ServiceFlags.None, null, null, true)
        {
            RotateWithEntity = false,
        };
        var selected = lua?.SelectedDockPort;
        state = state switch
        {
            ShipyardConsoleInterfaceState baseState => new ShipyardConsoleLuaDockSelectState(baseState, nav, selected),
            ParkingConsoleInterfaceState parkingState => new ParkingConsoleLuaDockSelectState(parkingState, nav, selected),
            _ => state
        };
    }
}
