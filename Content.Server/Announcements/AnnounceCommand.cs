using Content.Server._Lua.Announcements;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Audio;
using Robust.Shared.Console;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Server.Announcements;

[AdminCommand(AdminFlags.Moderator)]
public sealed class AnnounceCommand : LocalizedEntityCommands
{
    [Dependency] private readonly FactionAnnouncementSystem _factionAnnounce = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IResourceManager _res = default!;

    public override string Command => "announce";
    public override string Description => Loc.GetString("cmd-announce-desc");
    public override string Help => Loc.GetString("cmd-announce-help", ("command", Command));

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        switch (args.Length)
        {
            case 0:
                shell.WriteError(Loc.GetString("shell-need-minimum-one-argument"));
                return;
            case > 5:
                shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
                return;
        }

        var message = args[0];
        var factionId = args.Length >= 2 ? args[1] : FactionAnnouncementSystem.DefaultFactionId;
        var sectorId = args.Length >= 3 ? args[2] : FactionAnnouncementSystem.AllSectorsId;
        Color? color = null;
        SoundSpecifier? sound = null;

        if (args.Length >= 4)
        {
            try
            {
                color = Color.FromHex(args[3]);
            }
            catch
            {
                shell.WriteError(Loc.GetString("shell-invalid-color-hex"));
                return;
            }
        }

        if (args.Length >= 5)
            sound = new SoundPathSpecifier(args[4]);

        if (!_factionAnnounce.TryAnnounce(message, factionId, sectorId, sound, color))
        {
            shell.WriteError(Loc.GetString("cmd-announce-error-identity"));
            return;
        }

        shell.WriteLine(Loc.GetString("shell-command-success"));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHint(Loc.GetString("cmd-announce-arg-message")),
            2 => CompletionResult.FromHintOptions(
                _factionAnnounce.GetFactions().Select(f => f.Id),
                Loc.GetString("cmd-announce-arg-faction")),
            3 => CompletionResult.FromHintOptions(
                _factionAnnounce.GetSectors().Select(s => s.Id),
                Loc.GetString("cmd-announce-arg-sector")),
            4 => CompletionResult.FromHint(Loc.GetString("cmd-announce-arg-color")),
            5 => CompletionResult.FromHintOptions(
                CompletionHelper.AudioFilePath(args[4], _proto, _res),
                Loc.GetString("cmd-announce-arg-sound")
            ),
            _ => CompletionResult.Empty
        };
    }
}
