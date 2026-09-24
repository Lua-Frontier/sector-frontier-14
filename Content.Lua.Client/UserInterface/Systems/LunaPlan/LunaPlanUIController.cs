// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Client.Lua.LunaPlan;
using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client.Lua.UserInterface.Systems.LunaPlan;

[UsedImplicitly]
public sealed class LunaPlanUIController : UIController, IOnStateEntered<LobbyState>, IOnStateExited<LobbyState>
{
    private LunaPlanWindow? _board;
    private LobbyGui? _lobby;

    public void OnStateEntered(LobbyState state)
    {
        if (UIManager.ActiveScreen is LobbyGui lobby)
        {
            _lobby = lobby;
            _lobby.RoadmapPressed += ToggleBoard;
        }
    }

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
        if (_lobby != null)
        {
            _lobby.RoadmapPressed -= ToggleBoard;
            _lobby = null;
        }

        _board?.Close();
        _board = null;
    }
}
