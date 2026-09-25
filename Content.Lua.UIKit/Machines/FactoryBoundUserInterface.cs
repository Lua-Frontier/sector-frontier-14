// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Client.GameObjects;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.UserInterface;

public abstract class FactoryBoundUserInterface : BoundUserInterface
{
    protected FactoryBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected T OpenWindow<T>(T window) where T : BaseWindow
    {
        window.OnClose += Close;
        var ui = EntMan.System<UserInterfaceSystem>();
        ui.RegisterControl(this, window);
        if (ui.TryGetPosition(Owner, UiKey, out var position))
            window.Open(position);
        else
            window.OpenCentered();
        return window;
    }

    protected T OpenWindowCenteredLeft<T>(T window) where T : BaseWindow
    {
        window.OnClose += Close;
        var ui = EntMan.System<UserInterfaceSystem>();
        ui.RegisterControl(this, window);
        if (ui.TryGetPosition(Owner, UiKey, out var position))
            window.Open(position);
        else
            window.OpenCenteredLeft();
        return window;
    }

    protected T OpenWindowCenteredRight<T>(T window) where T : BaseWindow
    {
        window.OnClose += Close;
        var ui = EntMan.System<UserInterfaceSystem>();
        ui.RegisterControl(this, window);
        if (ui.TryGetPosition(Owner, UiKey, out var position))
            window.Open(position);
        else
            window.OpenCenteredRight();
        return window;
    }
}
