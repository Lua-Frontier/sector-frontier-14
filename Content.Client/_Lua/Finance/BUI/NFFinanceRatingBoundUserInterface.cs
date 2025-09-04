/*
 * LuaWorld - This file is licensed under AGPLv3
 * Copyright (c) 2025 LuaWorld Contributors
 * See AGPLv3.txt for details.
 */

using Content.Shared.UserInterface;
using Content.Shared._Lua.Finance.BUI;
using Content.Shared._Lua.Finance.Events;
using Robust.Client.UserInterface;
using Content.Client._Lua.Finance.UI; //Lua

namespace Content.Client._NF.Finance.BUI;

public sealed class NFFinanceRatingBoundUserInterface : BoundUserInterface
{
    public NFFinanceRatingBoundUserInterface(EntityUid owner, Enum key) : base(owner, key)
    {
    }

    private NFFinanceRatingWindow? _window;

    protected override void Open()
    {
        base.Open();
        if (_window != null)
        {
            _window.MoveToFront();
        }
        else
        {
            _window = new NFFinanceRatingWindow();
            _window.QueryRequested += () => SendMessage(new FinanceRatingQueryMessage(""));
            _window.OnClose += () => { _window = null; };
            _window.OpenCentered();
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;
        if (_window != null)
        {
            _window.Close();
            _window = null;
        }
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (_window == null)
            return;
        if (state is FinanceRatingState s)
            _window.UpdateState(s);
    }
}


