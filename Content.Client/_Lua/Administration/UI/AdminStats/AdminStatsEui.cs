// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaCorp
// See AGPLv3.txt for details.
using Content.Client.Eui;
using Content.Shared._Lua.Administration.AdminStats;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client._Lua.Administration.UI.AdminStats;

[UsedImplicitly]
public sealed class AdminStatsEui : BaseEui
{
    private AdminStatsWindow? _window;
    private bool _isResourceOnly;

    public override void Opened()
    {
        base.Opened();
        _window = new AdminStatsWindow();
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.OnFullRefresh += () => SendMessage(new AdminStatsEuiMsg.RefreshAllRequest());
        _window.OnResourceRefresh += () => SendMessage(new AdminStatsEuiMsg.RefreshResourcesRequest());
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();
        _window?.Dispose();
        _window = null;
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not AdminStatsEuiState statsState || _window == null) return;
        _window.UpdateFullState(statsState);
    }
}
