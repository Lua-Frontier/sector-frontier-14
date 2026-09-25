using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Traitor.Uplink.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class AddUplinkCommand : LocalizedEntityCommands
{
    public override string Command => "adduplink";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        shell.WriteLine("adduplink is disabled: syndicate gear is obtained via ResearchAndDevelopmentServerSyndicate / SyndicateTechFab.");
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return CompletionResult.Empty;
    }
}
