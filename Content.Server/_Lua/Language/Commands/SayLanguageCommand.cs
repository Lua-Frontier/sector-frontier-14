using Content.Server.Chat.Systems;
using Content.Shared.Administration;
using SharedIC = Content.Shared._Lua.InGameICChatType;
using Content.Shared.Chat;
using Robust.Shared.Console;
using Robust.Shared.Enums;

namespace Content.Server._Lua.Language.Commands;

[AnyCommand]
public sealed class SayLanguageCommand : IConsoleCommand
{
    public string Command => "saylang";
    public string Description => Loc.GetString("command-saylang-desc");
    public string Help => Loc.GetString("command-saylang-help", ("command", Command));

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

        if (args.Length < 2)
            return;

        var message = string.Join(" ", args, startIndex: 1, count: args.Length - 1).Trim();

        if (string.IsNullOrEmpty(message))
            return;

        var languages = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<LanguageSystem>();
        var chats = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<ChatSystem>();

        if (!SelectLanguageCommand.TryParseLanguageArgument(languages,
                playerEntity,
                args[0],
                out var failReason,
                out var language))
        {
            shell.WriteError(failReason);
            return;
        }

        // В текущем ChatSystem нет параметра languageOverride на этом уровне.
        chats.TrySendInGameICMessage(playerEntity,
            message,
            Content.Server.Chat.Systems.InGameICChatType.Speak,
            ChatTransmitRange.Normal,
            false,
            shell,
            player);
    }
}
