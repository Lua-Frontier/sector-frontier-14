// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Client.UserInterface;
using Content.Shared._NF.Bank.Events;
using Content.Shared._Lua.Bank.Events;
using Content.Shared._Lua.Bank.UI;

namespace Content.Client._Lua.Bank.UI;

public sealed class LuaATMMenuBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private LuaATMMenu? _menu;

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<LuaATMMenu>();

        _menu.WithdrawRequest += OnWithdraw;
        _menu.DepositRequest += OnDeposit;
    }

    private void OnWithdraw(int amount)
    {
        SendMessage(new BankWithdrawMessage(amount));
    }

    private void OnDeposit()
    {
        SendMessage(new BankDepositMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_menu == null || state is not LuaATMMenuInterfaceState bankState)
        {
            return;
        }

        _menu.UpdateState(bankState);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (_menu == null || message is not LuaATMPersonalInfoMessage personalMessage)
        {
            return;
        }

        _menu.UpdatePersonalState(personalMessage);
    }
}
