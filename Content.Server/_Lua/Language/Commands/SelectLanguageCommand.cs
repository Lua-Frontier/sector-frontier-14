using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Administration;
using Content.Shared._Lua.Language;
using Robust.Shared.Console;
using Robust.Shared.Enums;

namespace Content.Server._Lua.Language.Commands;

[AnyCommand]
public sealed class SelectLanguageCommand : IConsoleCommand
{
    public string Command => "languageselect";
    public string Description => Loc.GetString("command-language-select-desc");
    public string Help => Loc.GetString("command-language-select-help", ("command", Command));

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        if (player.Status != SessionStatus.InGame)
            return;

        if (player.AttachedEntity is not { } playerEntity)
        {
            shell.WriteError(Loc.GetString("shell-must-be-attached-to-entity"));
            return;
        }

        if (args.Length < 1)
            return;

        var languages = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<LanguageSystem>();

        if (!TryParseLanguageArgument(languages, playerEntity, args[0], out var failReason, out var language))
        {
            shell.WriteError(failReason);
            return;
        }

        languages.SetLanguage(playerEntity, language.ID);
    }

    public static bool TryParseLanguageArgument(
        LanguageSystem languageSystem,
        EntityUid speaker,
        string input,
        [NotNullWhen(false)] out string? failureReason,
        [NotNullWhen(true)] out LanguagePrototype? language)
    {
        failureReason = null;
        language = null;

        if (int.TryParse(input, out var num))
        {
            var spoken = languageSystem.GetSpokenLanguages(speaker);
            if (num > 0 && num - 1 < spoken.Count)
                language = languageSystem.GetLanguagePrototype(spoken[num - 1]);

            if (language != null)
                return true;

            failureReason = Loc.GetString("command-language-invalid-number", ("total", spoken.Count));
            return false;
        }
        else
        {
            language = languageSystem.GetLanguagePrototype(input);

            if (language != null && languageSystem.CanSpeak(speaker, language.ID))
                return true;

            failureReason = Loc.GetString("command-language-invalid-language", ("id", input));
            return false;
        }
    }
}
