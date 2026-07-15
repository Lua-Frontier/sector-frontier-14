using Content.Server.Administration;
using Content.Server._Lua.Administration.UI;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Lua.Administration.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class OpenAdminFactionWarCommand : LocalizedEntityCommands
{
    [Dependency] private readonly EuiManager _euiManager = default!;

    public override string Command => "adminfactionwar";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        var ui = new AdminFactionWarEui();
        _euiManager.OpenEui(ui, player);
    }
}
