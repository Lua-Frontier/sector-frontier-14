// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Client._Lua.LunaPlan;
using Content.Client.Lobby;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client._Lua.UserInterface.Systems.LunaPlan;

[UsedImplicitly]
public sealed class LunaPlanUIController : UIController, IOnStateExited<LobbyState>
{
    private LunaPlanWindow? _board;

    public void ToggleBoard()
    {
        if (_board == null || _board.Disposed)
        {
            _board = UIManager.CreateWindow<LunaPlanWindow>();
            _board.OnClose += () => _board = null;
        }

        if (_board.IsOpen)
            _board.Close();
        else
            _board.OpenCentered();
    }

    public void OnStateExited(LobbyState state)
    {
        _board?.Close();
        _board = null;
    }
}
