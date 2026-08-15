// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Client.Eui;
using Content.Shared._Lua.Administration.AlertLevel;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client._Lua.Administration.UI.AlertLevel;

[UsedImplicitly]
public sealed class AlertLevelAdminEui : BaseEui
{
    private AlertLevelAdminWindow? _window;

    public override void Opened()
    {
        base.Opened();
        _window = new AlertLevelAdminWindow();
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.RefreshPressed += () => SendMessage(new AlertLevelAdminEuiMsg.RefreshRequest());
        _window.ApplySectorPressed += (sectorId, level, locked) =>
            SendMessage(new AlertLevelAdminEuiMsg.SetSectorRequest
            {
                SectorId = sectorId,
                Level = level,
                Locked = locked,
            });
        _window.ApplyGlobalPressed += (level, locked) =>
            SendMessage(new AlertLevelAdminEuiMsg.SetGlobalRequest
            {
                Level = level,
                Locked = locked,
            });
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
        if (state is not AlertLevelAdminEuiState alertState || _window == null)
            return;

        _window.UpdateState(alertState);
    }
}
