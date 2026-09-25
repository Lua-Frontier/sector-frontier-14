// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server.Administration;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Lua.Server.AlertLevel.Commands;

[AdminCommand(AdminFlags.Fun)]
public sealed class SetAlertLevelCommand : LocalizedEntityCommands
{
    [Dependency] private readonly EuiManager _euiManager = default!;
    [Dependency] private readonly IAlertLevelAdminEuiFactory _alertLevelEui = default!;

    public override string Command => "setalertlevel";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        _euiManager.OpenEui(_alertLevelEui.Create(), player);
    }
}
