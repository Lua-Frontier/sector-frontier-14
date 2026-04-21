// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Lua.Stargate;
using Robust.Client.UserInterface;

namespace Content.Client._Lua.Stargate;

public sealed class StargateConsoleBoundUserInterface : BoundUserInterface
{
    private StargateConsoleWindow? _window;

    public StargateConsoleBoundUserInterface(EntityUid owner, Enum key) : base(owner, key)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<StargateConsoleWindow>();

        _window.OnSymbolPressed += symbol =>
        {
            SendMessage(new StargateInputSymbolMessage(symbol));
        };

        _window.OnDial += symbols =>
        {
            SendMessage(new StargateDialMessage(symbols));
        };

        _window.OnClear += () =>
        {
            SendMessage(new StargateClearInputMessage());
        };

        _window.OnClosePortal += () =>
        {
            SendMessage(new StargateClosePortalMessage());
        };

        _window.OnSaveDiskAddress += () =>
        {
            SendMessage(new StargateSaveDiskAddressMessage());
        };

        _window.OnDeleteDiskAddress += index =>
        {
            SendMessage(new StargateDeleteDiskAddressMessage(index));
        };

        _window.OnToggleIris += () =>
        {
            SendMessage(new StargateToggleIrisMessage());
        };

        _window.OnAutoDialFromDisk += address =>
        {
            SendMessage(new StargateAutoDialFromDiskMessage(address));
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is StargateConsoleUiState s)
            _window?.UpdateState(s);
    }
}
