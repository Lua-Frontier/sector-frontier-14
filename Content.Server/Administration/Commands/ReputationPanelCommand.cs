using System.Linq;
using Content.Server._Lua.Reputation;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared.Administration;
using Content.Shared.Database;
using Robust.Server.Player;
using Robust.Shared.Console;

namespace Content.Server.Administration.Commands;

[AnyCommand]
public sealed class ReputationPanelCommand : LocalizedCommands
{
    [Dependency] private readonly IAdminManager _admins = default!;
    [Dependency] private readonly IPlayerLocator _locator = default!;
    [Dependency] private readonly EuiManager _euis = default!;
    [Dependency] private readonly IPlayerManager _players = default!;

    public override string Command => "reputationpanel";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } admin)
        {
            shell.WriteError("This command cannot be run from the server console.");
            return;
        }

        if (args.Length != 2 || !TryParseKind(args[0], out var kind))
        {
            shell.WriteError("Usage: reputationpanel <player|admin> <name or user id>");
            return;
        }

        var adminData = _admins.GetAdminData(admin);
        if (kind == ReputationTargetKind.Player && adminData?.CanModeratePlayerReputation() != true ||
            kind == ReputationTargetKind.Admin && adminData?.CanModerateAdminReputation() != true)
        {
            shell.WriteError("You do not have permission to moderate this reputation type.");
            return;
        }

        var target = await _locator.LookupIdByNameOrIdAsync(args[1]);
        if (target == null)
        {
            shell.WriteError("Player not found.");
            return;
        }

        _euis.OpenEui(new ReputationModerationEui(kind, target.UserId, target.Username), admin);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(new[] { "player", "admin" }, "Reputation type");
        }

        if (args.Length == 2)
        {
            var options = _players.Sessions.OrderBy(c => c.Name).Select(c => c.Name).ToArray();
            return CompletionResult.FromHintOptions(options, "Player name or user id");
        }

        return CompletionResult.Empty;
    }

    private static bool TryParseKind(string value, out ReputationTargetKind kind)
    {
        switch (value.ToLowerInvariant())
        {
            case "player":
            case "players":
            case "игрок":
            case "игроки":
                kind = ReputationTargetKind.Player;
                return true;
            case "admin":
            case "admins":
            case "админ":
            case "админы":
                kind = ReputationTargetKind.Admin;
                return true;
            default:
                kind = default;
                return false;
        }
    }
}