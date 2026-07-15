// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server.EUI;
using Content.Shared._Lua.Company;
using Content.Shared.Eui;
using Robust.Shared.Player;

namespace Content.Server._Lua.Company;

public sealed class CompanyBriefingEui : BaseEui
{
    private readonly Action<ICommonSession, CompanyBriefingEui>? _onClosed;
    private readonly CompanyBriefingEuiState _state;

    public CompanyBriefingEui(string title, Color color, string text, Action<ICommonSession, CompanyBriefingEui>? onClosed = null)
    {
        _onClosed = onClosed;
        _state = new CompanyBriefingEuiState(title, color, text);
    }

    public override void Opened()
    {
        base.Opened();
        StateDirty();
    }

    public override EuiStateBase GetNewState()
    {
        return _state;
    }

    public override void Closed()
    {
        base.Closed();
        _onClosed?.Invoke(Player, this);
    }
}
