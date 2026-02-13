// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaCorp
// See AGPLv3.txt for details.
using Content.Server.Administration;
using Content.Server._Lua.Administration.UI;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Lua.Administration.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class OpenChunkMonitorCommand : LocalizedEntityCommands
{
    [Dependency] private readonly EuiManager _euiManager = default!;

    public override string Command => "chunkmonitor";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }
        var ui = new ChunkMonitorEui();
        _euiManager.OpenEui(ui, player);
    }
}
