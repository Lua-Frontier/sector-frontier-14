// LuaWorld/LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld/LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Lua.Parking;
using Robust.Client.UserInterface;

namespace Content.Client._Lua.Parking.UI;

public sealed class TrafficManagerTabletBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private TrafficManagerTabletWindow? _window;

    public TrafficManagerTabletBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<TrafficManagerTabletWindow>();
        _window.OnAction += SendAction;
        _window.OnClose += Close;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is TrafficManagerTabletUiState st) _window?.UpdateState(st);
    }

    private void SendAction(TrafficManagerTabletAction action, NetEntity shuttle)
    { SendMessage(new TrafficManagerTabletUiMessage(action, shuttle)); }
}


