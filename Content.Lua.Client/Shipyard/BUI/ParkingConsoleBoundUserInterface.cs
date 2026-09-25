// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Client.Lua.Shipyard.UI;
using Content.Lua.Shared.Achievements;
using Content.Lua.Shared.Shipyard.BUI;
using Content.Lua.Shared.Shipyard.BUIStates;
using Content.Lua.Shared.Shipyard.Events;
using Content.Shared._NF.Shipyard.Events;
using Content.Shared.Containers.ItemSlots;
using Robust.Client.UserInterface;
using Robust.Shared.Network;

namespace Content.Client.Lua.Shipyard.BUI;

public sealed class ParkingConsoleBoundUserInterface : BoundUserInterface
{
    private ParkingConsoleMenu? _menu;
    [Dependency] private readonly INetManager _net = default!;

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
        _menu.SetRadarConsole(Owner);
        _net.ClientSendMessage(new TryUnlockAchievementMessage(AchievementIds.ComputerShipyardParking));
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
