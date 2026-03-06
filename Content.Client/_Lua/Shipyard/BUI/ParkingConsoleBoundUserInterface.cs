// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Client._Lua.Shipyard.UI;
using Content.Shared._Lua.Shipyard.BUI;
using Content.Shared._Lua.Shipyard.BUIStates;
using Content.Shared._Lua.Shipyard.Events;
using Content.Shared._NF.Shipyard.Events;
using Content.Shared.Containers.ItemSlots;
using Robust.Client.UserInterface;

namespace Content.Client._Lua.Shipyard.BUI;

public sealed class ParkingConsoleBoundUserInterface : BoundUserInterface
{
    private ParkingConsoleMenu? _menu;

    public ParkingConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        if (_menu != null) return;
        _menu = this.CreateWindow<ParkingConsoleMenu>();
        _menu.OnPark += _ => SendMessage(new ShipyardConsoleSellMessage());
        _menu.OnRecall += _ => SendMessage(new ShipyardConsolePurchaseMessage(string.Empty));
        _menu.OnDockPortSelected += port => SendMessage(new SelectDockPortMessage(port));
        _menu.TargetIdButton.OnPressed += _ => SendMessage(new ItemSlotButtonPressedEvent("ShipyardConsole-targetId"));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        ParkingConsoleInterfaceState? baseState = null;
        ParkingConsoleLuaDockSelectState? dockState = null;
        if (state is ParkingConsoleLuaDockSelectState lua)
        {
            baseState = lua.BaseState;
            dockState = lua;
        }
        else if (state is ParkingConsoleInterfaceState plain)
        { baseState = plain; }
        else
        { return; }
        _menu?.UpdateState(baseState);
        _menu?.UpdateDockSelect(dockState?.DockNavState, dockState?.SelectedDockPort);
    }
}
