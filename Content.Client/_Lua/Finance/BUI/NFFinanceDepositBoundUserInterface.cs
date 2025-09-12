/*
 * LuaWorld - This file is licensed under AGPLv3
 * Copyright (c) 2025 LuaWorld Contributors
 * See AGPLv3.txt for details.
 */
using Content.Shared._Lua.Finance.Events;
using Content.Client._Lua.Finance.UI; //Lua

namespace Content.Client._NF.Finance.BUI;

public sealed class NFFinanceDepositBoundUserInterface : BoundUserInterface
{
    public NFFinanceDepositBoundUserInterface(EntityUid owner, Enum key) : base(owner, key)
    {
    }

    private NFFinanceDepositWindow? _window;

    protected override void Open()
    {
        base.Open();
        if (_window != null)
        {
            _window.MoveToFront();
        }
        else
        {
            _window = new NFFinanceDepositWindow();
            _window.OpenRequested += (amt, term, rate) => SendMessage(new FinanceOpenDepositRequestMessage(amt, term, rate));
            _window.CloseRequested += id => SendMessage(new FinanceCloseDepositRequestMessage(id));
            _window.TopUpRequested += (id, amt) => SendMessage(new FinanceTopUpDepositRequestMessage(id, amt));
            _window.PartialWithdrawRequested += (id, amt) => SendMessage(new FinancePartialWithdrawDepositRequestMessage(id, amt));
            _window.RefreshRequested += () => { SendMessage(new FinanceStatusQueryMessage()); SendMessage(new FinanceDepositListQueryMessage()); };
            _window.OnClose += () => { _window = null; };
            _window.OpenCentered();
        }
        SendMessage(new FinanceStatusQueryMessage());
        SendMessage(new FinanceDepositListQueryMessage());
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
        if (state is FinanceStatusState s)
            _window.UpdateState(s);
        else if (state is FinanceDepositListState list)
        {
            _window.UpdateDeposits(list);
            // Дополнительно запрашиваем статус, если он по какой-то причине не пришёл раньше
            SendMessage(new FinanceStatusQueryMessage());
        }
    }
}


