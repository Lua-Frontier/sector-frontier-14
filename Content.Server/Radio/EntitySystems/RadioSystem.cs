using Content.Server._Lua.Language; // Lua
using Content.Server._NF.Radio; // Frontier
using Content.Server.Administration.Logs;
using Content.Server.Chat.Systems;
using Content.Server.Power.Components;
using Content.Server.Radio.Components;
using Content.Shared._Lua.Language;
using Content.Shared.Access.Components;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Ghost; // Nuclear-14
using Content.Shared.Inventory;
using Content.Shared.PDA;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Speech;
using Robust.Server.GameObjects; // Frontier
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Replays;
using Robust.Shared.Utility;
using System.Text.RegularExpressions;

namespace Content.Server.Radio.EntitySystems;

/// <summary>
///     This system handles intrinsic radios and the general process of converting radio messages into chat messages.
/// </summary>
public sealed class RadioSystem : EntitySystem
{
    [Dependency] private readonly INetManager _netMan = default!;
    [Dependency] private readonly IReplayRecordingManager _replay = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly LanguageSystem _language = default!; // Lua

    // set used to prevent radio feedback loops.
    private readonly HashSet<string> _messages = new();

    private EntityQuery<TelecomExemptComponent> _exemptQuery;

    private readonly Dictionary<string, string[]> _departments = new Dictionary<string, string[]>
    {
        { "fcdf03", ["командование", "кэп", "капитан", "глава персонала"] },
        { "d98b71", ["юридический отдел", "магистрат", "юрист", "агент внутренних дел"] },
        { "1563bd", ["служба безопасности", "бриг", "варден", "смотритель", "инструктор", "детектив", "пилот сб", "бригмед", "кадет"] },
        { "57b8f0", ["медицинский отдел", "главный врач", "ведущий врач", "химик", "врач", "парамед", "патологоанатом", "психолог", "интерн"] },
        { "c68cfa", ["научный отдел", "рнд", "нио", "научный руководитель", "ведущий учёный", "учёный", "робоёб", "лаборант", "анома"] },
        { "f2ac26", ["инженерный отдел", "инженерный", "старший инженер", "ведущий инженер", "атмосферный техник", "атмос", "инженер", "инженер стажёр"] },
        { "a46106", ["отдел снабжения", "карго", "каргонцы", "ведущий утилизатор", "ведущий утиль", "утиль", "утилизатор", "грузчик"] },
        { "6ca729", ["сервисный отдел", "сервис", "менеджер", "шеф", "повар", "ботаник", "бармен", "боксер", "уборщик", "библиотекарь", "священик", "святой отец", "зоотехник", "репортёр", "музыкант"] },
        { "2ed2fd", ["искусственный интеллект", "юнит", "борг"] },
        { "fb77f3", ["клуня", "клоун"] },
        { "d0d0d0", ["мим"] }
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IntrinsicRadioReceiverComponent, RadioReceiveEvent>(OnIntrinsicReceive);
        SubscribeLocalEvent<IntrinsicRadioTransmitterComponent, EntitySpokeEvent>(OnIntrinsicSpeak);

        _exemptQuery = GetEntityQuery<TelecomExemptComponent>();
    }

    private void OnIntrinsicSpeak(EntityUid uid, IntrinsicRadioTransmitterComponent component, EntitySpokeEvent args)
    {
        if (args.Channel != null && component.Channels.Contains(args.Channel.ID))
        {
            SendRadioMessage(uid, args.Message, args.Channel, uid, language: args.Language);
            args.Channel = null; // prevent duplicate messages from other listeners.
        }
    }

    //Nuclear-14
    /// <summary>
    /// Gets the message frequency, if there is no such frequency, returns the standard channel frequency.
    /// </summary>
    public int GetFrequency(EntityUid source, RadioChannelPrototype channel)
    {
        if (TryComp<RadioMicrophoneComponent>(source, out var radioMicrophone))
            return radioMicrophone.Frequency;

        return channel.Frequency;
    }

    private void OnIntrinsicReceive(EntityUid uid, IntrinsicRadioReceiverComponent component, ref RadioReceiveEvent args)
    {
        if (TryComp(uid, out ActorComponent? actor))
        {
            var msg = args.ChatMsg;
            if (args.Language != null && args.LanguageObfuscatedChatMsg != null && !_language.CanUnderstand(uid, args.Language.ID)) msg = args.LanguageObfuscatedChatMsg;
            _netMan.ServerSendMessage(msg, actor.PlayerSession.Channel);
        }

    }
    /// <summary>
    /// Send radio message to all active radio listeners
    /// </summary>
    public void SendRadioMessage(EntityUid messageSource, string message, ProtoId<RadioChannelPrototype> channel, EntityUid radioSource, LanguagePrototype? language = null, int? frequency = null, bool escapeMarkup = true) // Frontier: added frequency
    {
        SendRadioInternal(messageSource, message, _prototype.Index(channel), radioSource, frequency: frequency, escapeMarkup: escapeMarkup, ignoreRange: false, language: language); // Frontier: added frequency // Lua SendRadioMessage<SendRadioInternal add ignoreRange
    }

    // Lua Global Radio start
    public void SendRadioMessageGlobal(EntityUid messageSource, string message, RadioChannelPrototype channel, EntityUid radioSource, LanguagePrototype? language = null, int? frequency = null, bool escapeMarkup = true)
    {
        SendRadioInternal(messageSource, message, channel, radioSource, frequency, escapeMarkup, ignoreRange: true, language: language);
    }
    // Lua Global Radio end

    /// <summary>
    /// Send radio message to all active radio listeners
    /// </summary>
    /// <param name="messageSource">Entity that spoke the message</param>
    /// <param name="radioSource">Entity that picked up the message and will send it, e.g. headset</param>
    public void SendRadioInternal(EntityUid messageSource, string message, RadioChannelPrototype channel, EntityUid radioSource, int? frequency, bool escapeMarkup, bool ignoreRange, LanguagePrototype? language = null) // Nuclear-14: add frequency // Lua SendRadioMessage<SendRadioInternal add ignoreRange
    {
        // TODO if radios ever garble / modify messages, feedback-prevention needs to be handled better than this.
        if (!_messages.Add(message))
            return;

        var evt = new TransformSpeakerNameEvent(messageSource, MetaData(messageSource).EntityName);
        RaiseLocalEvent(messageSource, evt);

        // Frontier: add name transform event
        var transformEv = new RadioTransformMessageEvent(channel, radioSource, evt.VoiceName, message, messageSource);
        RaiseLocalEvent(radioSource, ref transformEv);
        message = transformEv.Message;
        messageSource = transformEv.MessageSource;
        // End Frontier

        var name = transformEv.Name; // Frontier: evt.VoiceName<transformEv.Name
        name = FormattedMessage.EscapeText(name);

        SpeechVerbPrototype speech;
        if (evt.SpeechVerb != null && _prototype.TryIndex(evt.SpeechVerb, out var evntProto))
            speech = evntProto;
        else
            speech = _chat.GetSpeechVerb(messageSource, message);

        var content = escapeMarkup
            ? FormattedMessage.EscapeText(message)
            : message;

        var headsetColor = TryComp(radioSource, out HeadsetComponent? headset) ? headset.Color : channel.Color;

        var job = String.Empty;
        if (_inventory.HasSlot(messageSource, "id"))
        {
            job = Loc.GetString("chat-radio-source-unknown");

            if (_inventory.TryGetSlotEntity(messageSource, "id", out var idSlotEntity))
            {
                if (TryComp(idSlotEntity, out PdaComponent? pda))
                    idSlotEntity = pda.ContainedId;

                job = TryComp(idSlotEntity, out IdCardComponent? idCard) && !string.IsNullOrEmpty(idCard.LocalizedJobTitle)
                    ? _chat.SanitizeMessageCapital(idCard.LocalizedJobTitle)
                    : Loc.GetString("chat-radio-source-unknown");
            }

            job = $"\\[{job}\\] ";
        }

        content = Highlight(content);

        // Frontier: append frequency if the channel requests it
        string channelText;
        if (channel.ShowFrequency)
            channelText = $"\\[{channel.LocalizedName} ({frequency})\\]";
        else
            channelText = $"\\[{channel.LocalizedName}\\]";
        // End Frontier

        var wrappedMessage = WrapRadioMessage(messageSource, speech, channel, name, content, headsetColor, job, language);

        // most radios are relayed to chat, so lets parse the chat message beforehand
        var chat = new ChatMessage(
            ChatChannel.Radio,
            message,
            wrappedMessage,
            NetEntity.Invalid,
            null);
        var chatMsg = new MsgChatMessage { Message = chat };
        MsgChatMessage? notUdsMsg = null;
        if (language != null)
        {
            var obf = _language.ObfuscateSpeech(content, language);
            var wrappedObf = WrapRadioMessage(messageSource, speech, channel, name, obf, headsetColor, job, language);
            var chatObf = new ChatMessage(ChatChannel.Radio, obf, wrappedObf, NetEntity.Invalid, null);
            notUdsMsg = new MsgChatMessage { Message = chatObf };
        }
        var ev = new RadioReceiveEvent(message, messageSource, channel, radioSource, chatMsg, [])
        {
            Language = language,
            LanguageObfuscatedChatMsg = notUdsMsg
        };

        var sendAttemptEv = new RadioSendAttemptEvent(channel, radioSource);
        RaiseLocalEvent(ref sendAttemptEv);
        RaiseLocalEvent(radioSource, ref sendAttemptEv);
        var canSend = !sendAttemptEv.Cancelled;

        var sourceMapId = Transform(radioSource).MapID;
        //Lua mod global or local server start
        var hasActiveServer = ignoreRange
                    ? HasActiveServerGlobal(channel.ID)
                    : HasActiveServer(sourceMapId, channel.ID);
        //Lua mod global or local server end
        var sourceServerExempt = _exemptQuery.HasComp(radioSource);

        var radioQuery = EntityQueryEnumerator<ActiveRadioComponent, TransformComponent>();

        if (frequency == null) // Nuclear-14
            frequency = GetFrequency(messageSource, channel); // Nuclear-14

        while (canSend && radioQuery.MoveNext(out var receiver, out var radio, out var transform))
        {
            if (!radio.ReceiveAllChannels)
            {
                radio.Channels ??= new HashSet<string>();
                if (!radio.Channels.Contains(channel.ID)) continue;
                if (TryComp<IntercomComponent>(receiver, out var intercom))
                {
                    intercom.SupportedChannels ??= new List<ProtoId<RadioChannelPrototype>>();
                    if (!intercom.SupportedChannels.Contains(channel.ID)) continue;
                }
            }
            else if (TryComp<IntercomComponent>(receiver, out var intercom) && intercom.SupportedChannels != null)
            {
                if (!intercom.SupportedChannels.Contains(channel.ID))
                    continue;
            }

            // Lua start global
            if (!ignoreRange)
            {
                if (!HasComp<GhostComponent>(receiver) && GetFrequency(receiver, channel) != frequency)
                    continue;

                if (!channel.LongRange && transform.MapID != sourceMapId && !radio.GlobalReceive)
                    continue;
            }
            // Lua end global

            // don't need telecom server for long range channels or handheld radios and intercoms
            var needServer = !ignoreRange && !channel.LongRange && !sourceServerExempt; // lua add ignoreRange
            if (needServer && !hasActiveServer)
                continue;

            // check if message can be sent to specific receiver
            var attemptEv = new RadioReceiveAttemptEvent(channel, radioSource, receiver);
            RaiseLocalEvent(ref attemptEv);
            RaiseLocalEvent(receiver, ref attemptEv);
            if (attemptEv.Cancelled)
                continue;
            var listener = ResolveListener(receiver);
            var perMsg = ev.ChatMsg;
            if (language != null && ev.LanguageObfuscatedChatMsg != null && !_language.CanUnderstand(listener, language.ID)) perMsg = ev.LanguageObfuscatedChatMsg;
            var evPer = new RadioReceiveEvent(ev.Message, ev.MessageSource, ev.Channel, ev.RadioSource, perMsg, ev.Receivers)
            {
                Language = ev.Language,
                LanguageObfuscatedChatMsg = ev.LanguageObfuscatedChatMsg
            };
            RaiseLocalEvent(receiver, ref evPer);
        }
        RaiseLocalEvent(new RadioSpokeEvent(messageSource, message, ev.Receivers.ToArray()));
        if (name != Name(messageSource)) _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Radio message from {ToPrettyString(messageSource):user} as {name} on {channel.LocalizedName}: {message}");
        else _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Radio message from {ToPrettyString(messageSource):user} on {channel.LocalizedName}: {message}");
        _replay.RecordServerMessage(chat);
        _messages.Remove(message);
    }
    private string WrapRadioMessage(EntityUid source, SpeechVerbPrototype speech, RadioChannelPrototype channel, string name, string message, Color headsetColor, string job, LanguagePrototype? language)
    {
        if (language?.SpeechOverride.Color is { } colorOverride)
        {
            var mixed = Color.InterpolateBetween(channel.Color, colorOverride, colorOverride.A);
            message = Loc.GetString("chat-radio-wrap-language-color",
                ("message", message),
                ("color", channel.Color),
                ("languageColor", mixed));
        }

        var fontId = language?.SpeechOverride.FontId ?? speech.FontId;
        var fontSize = language?.SpeechOverride.FontSize ?? speech.FontSize;
        if (language?.SpeechOverride?.FontId != null || language?.SpeechOverride?.FontSize != null)
        {
            var fontSizeInt = (int) Math.Round((double) fontSize);
            message = Loc.GetString("chat-manager-wrap-language-font",
                ("message", message),
                ("fontType", fontId),
                ("fontSize", fontSizeInt));
        }
        return Loc.GetString(speech.Bold ? "chat-radio-message-wrap-bold" : "chat-radio-message-wrap",
            ("channel-color", channel.Color),
            ("fontType", fontId),
            ("fontSize", fontSize),
            ("verb", Loc.GetString(_random.Pick(speech.SpeechVerbStrings))),
            ("channel", $"\\[{channel.LocalizedName}\\]"),
            ("name", name),
            ("message", message),
            ("headset-color", headsetColor),
            ("job", job));
    }

    private EntityUid ResolveListener(EntityUid receiver)
    {
        try
        {
            var current = receiver;
            var xforms = GetEntityQuery<TransformComponent>();
            while (true)
            {
                if (HasComp<ActorComponent>(current)) return current;

                if (!xforms.TryGetComponent(current, out var xform)) break;

                var parent = xform.ParentUid;
                if (!parent.IsValid() || parent == current) break;
                current = parent;
            }
        }
        catch
        { }
        return receiver;
    }

    /// <inheritdoc cref="TelecomServerComponent"/>
    private bool HasActiveServer(MapId mapId, string channelId)
    {
        var servers = EntityQuery<TelecomServerComponent, EncryptionKeyHolderComponent, ApcPowerReceiverComponent, TransformComponent>();
        foreach (var (_, keys, power, transform) in servers)
        {
            if (transform.MapID == mapId &&
                power.Powered &&
                keys.Channels.Contains(channelId))
            {
                return true;
            }
        }
        return false;
    }

    // Lua global server start
    private bool HasActiveServerGlobal(string channelId)
    {
        var servers = EntityQuery<TelecomServerComponent, EncryptionKeyHolderComponent, ApcPowerReceiverComponent, TransformComponent>();
        foreach (var (_, keys, power, _) in servers)
        {
            if (!power.Powered)
                continue;
            if (!keys.Channels.Contains(channelId))
                continue;
            return true;
        }
        return false;
    }
    // Lua global server end

    private string Highlight(string msg)
    {

        foreach (var department in _departments)
        {
            string color = department.Key;
            foreach (string word in department.Value)
            {
                string redex_word = RedexWord(word);

                Regex regex = new Regex($@"\w*{redex_word}\w*", RegexOptions.IgnoreCase);
                MatchCollection matches = regex.Matches(msg);

                foreach (Match match in matches)
                {
                    msg = msg.Replace(match.Value, "[color=#" + color + "]" + match.Value + "[/color]");
                }
            }
        }
        return msg;
    }

    private string RedexWord(string word)
    {
        string redex_word = "";
        foreach (char letter in word)
        {
            string add_letter = letter.ToString();
            if (letter == 'л')
                add_letter = "[лв]";
            if (letter == 'р')
                add_letter = "[рв]";
            if (letter == 'ы')
                add_letter = "[иы]";
            redex_word += add_letter + "+";
        }

        return redex_word.Remove(redex_word.Length - 1);
    }
}
