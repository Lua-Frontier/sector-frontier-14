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

public sealed class NFFinanceLoansBoundUserInterface : BoundUserInterface
{
	public NFFinanceLoansBoundUserInterface(EntityUid owner, Enum key) : base(owner, key)
	{
	}

	private NFFinanceLoansWindow? _window;

	protected override void Open()
	{
		base.Open();
		if (_window != null)
		{
			_window.MoveToFront();
		}
		else
		{
			_window = new NFFinanceLoansWindow();
			_window.OnRefresh += () => SendMessage(new FinanceLoansQueryMessage());
			_window.OnRefreshDeposits += () => SendMessage(new FinanceDepositsQueryMessage());
			_window.OnForceCloseRequested += id => SendMessage(new FinanceCloseDepositRequestMessage(id));
			_window.OnClose += () => { _window = null; };
			_window.OpenCentered();
		}
		SendMessage(new FinanceLoansQueryMessage());
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
		switch (state)
		{
			case FinanceLoansConfigState cfg:
				_window.ConfigureTabs(cfg.ShowLoans, cfg.ShowDeposits, cfg.AllowForceClose);
				break;
			case FinanceLoansState s:
				_window.UpdateRows(s.Rows);
				break;
			case FinanceDepositsState ds:
				_window.UpdateDepositRows(ds.Rows);
				break;
		}
	}
}
