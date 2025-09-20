using Content.Shared._Lua.Chat.Systems;
using Content.Shared._Lua.Language;
using Robust.Shared.Physics;
using Robust.Shared.Utility;
using Robust.Shared.Random;

namespace Content.Server.Chat.Systems;
public partial class ChatSystem
{
    public string WrapPublicMessage(EntityUid source, string name, string message, LanguagePrototype? language = null)
    {
        var wrapId = GetSpeechVerb(source, message).Bold ? "chat-manager-entity-say-bold-wrap-message" : "chat-manager-entity-say-wrap-message";
        return WrapMessage(wrapId, InGameICChatType.Speak, source, name, message, language);
    }

    public string WrapWhisperMessage(EntityUid source, LocId defaultWrap, string entityName, string message, LanguagePrototype? language = null)
    {
        return WrapMessage(defaultWrap, InGameICChatType.Whisper, source, entityName, message, language);
    }

    public string WrapMessage(LocId wrapId, InGameICChatType chatType, EntityUid source, string entityName, string message, LanguagePrototype? language)
    {
        language ??= _language.GetLanguage(source);
        if (language.SpeechOverride.MessageWrapOverrides.TryGetValue(chatType, out var wrapOverride))
            wrapId = wrapOverride;

        var speech = GetSpeechVerb(source, message);
        var verbId = language.SpeechOverride.SpeechVerbOverrides is { } verbsOverride
            ? _random.Pick(verbsOverride).ToString()
            : _random.Pick(speech.SpeechVerbStrings);

        message = FormattedMessage.EscapeText(message);

        if (language.SpeechOverride.Color is { } colorOverride)
        {
            var color = Color.InterpolateBetween(DefaultSpeakColor, colorOverride, colorOverride.A);
            message = Loc.GetString("chat-manager-wrap-language-color",
                ("message", message),
                ("color", color));
        }

        var fontType = language.SpeechOverride.FontId ?? speech.FontId;
        var fontSize = language.SpeechOverride.FontSize ?? speech.FontSize;
        if (language.SpeechOverride.FontId != null || language.SpeechOverride.FontSize != null)
        {
            var fontKey = chatType == InGameICChatType.Whisper
                ? "chat-manager-wrap-language-font-whisper"
                : "chat-manager-wrap-language-font";
            message = Loc.GetString(fontKey,
                ("message", message),
                ("fontType", fontType),
                ("fontSize", fontSize));
        }

        if (chatType == InGameICChatType.Speak)
        {
            return Loc.GetString(wrapId,
                ("entityName", entityName),
                ("verb", Loc.GetString(verbId)),
                ("fontType", fontType),
                ("fontSize", fontSize),
                ("message", message));
        }

        return Loc.GetString(wrapId,
            ("entityName", entityName),
            ("verb", Loc.GetString(verbId)),
            ("message", message));
    }

    private bool CheckAttachedGrids(EntityUid source, EntityUid receiver)
    {
        if (!TryComp<JointComponent>(Transform(source).GridUid, out var sourceJoints)
            || !TryComp<JointComponent>(Transform(receiver).GridUid, out var receiverJoints))
            return false;

        foreach (var (id, _) in sourceJoints.GetJoints)
        {
            if (receiverJoints.GetJoints.ContainsKey(id))
                return true;
        }

        return false;
    }
}
