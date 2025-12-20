using System;
using Content.Server.Administration;
using Content.Server.Sponsors;
using Content.Shared.Administration;
using Content.Shared._Lua.SponsorLoadout;
using Robust.Server.Player;
using Robust.Shared.Console;

namespace Content.Server._Lua.Administration.Commands;

[AdminCommand(AdminFlags.Host)]
public sealed class DonateAddCommand : LocalizedCommands
{
    [Dependency] private readonly IPlayerLocator _locator = default!;
    [Dependency] private readonly SponsorManager _sponsors = default!;

    public override string Command => "donateadd";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 3)
        {
            shell.WriteError("Использование: donateadd <игрок> <роль> <дней>");
            shell.WriteError("  <дней> = 0 — бессрочно");
            shell.WriteError("  <дней> = N — донат на N дней от текущего момента");
            return;
        }

        var playerName = args[0];
        var roleInput = args[1];
        var daysString = args[2];

        if (!int.TryParse(daysString, out var days) || days < 0)
        {
            shell.WriteError($"Неверное количество дней '{daysString}'. Укажите 0 (бессрочно) или положительное число.");
            return;
        }

        var role = NormalizeRole(roleInput);
        if (role == null)
        {
            shell.WriteError($"Неизвестная группа доната '{roleInput}'. Используйте '{DonorGroups.Shareholder}' или '{DonorGroups.God}'.");
            return;
        }

        var data = await _locator.LookupIdByNameOrIdAsync(playerName);
        if (data is null)
        {
            shell.WriteError(Loc.GetString("cmd-whitelistadd-not-found", ("username", playerName)));
            return;
        }

        DateTimeOffset? plannedEnd = days == 0 ? null : DateTimeOffset.UtcNow.AddDays(days);

        await _sponsors.AddSponsorAsync(data.UserId, data.Username, role, plannedEnd);

        if (plannedEnd == null)
            shell.WriteLine($"Игрок {data.Username} получил роль донатёра '{role}' бессрочно.");
        else
            shell.WriteLine($"Игрок {data.Username} получил роль донатёра '{role}' на {days} дней (до {plannedEnd:dd.MM.yyyy HH:mm} UTC).");
    }

    private static string? NormalizeRole(string roleInput)
    {
        var lower = roleInput.ToLowerInvariant();
        return lower switch
        {
            "акционер" or "shareholder" => DonorGroups.Shareholder,
            "божество" or "god" => DonorGroups.God,
            _ => null
        };
    }
}

[AdminCommand(AdminFlags.Host)]
public sealed class DonateRemoveCommand : LocalizedCommands
{
    [Dependency] private readonly IPlayerLocator _locator = default!;
    [Dependency] private readonly SponsorManager _sponsors = default!;

    public override string Command => "donateremove";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1)
        {
            shell.WriteError(Loc.GetString("shell-need-minimum-one-argument"));
            shell.WriteLine(Help);
            return;
        }

        var playerName = string.Join(' ', args).Trim();

        var data = await _locator.LookupIdByNameOrIdAsync(playerName);
        if (data is null)
        {
            shell.WriteError(Loc.GetString("cmd-whitelistadd-not-found", ("username", playerName)));
            return;
        }

        await _sponsors.RemoveSponsorAsync(data.UserId);
        shell.WriteLine($"Донат для игрока {data.Username} завершён, VIP-пассажир выключен.");
    }
}


