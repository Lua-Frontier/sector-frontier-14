using Content.Server.Chat.Systems;
using Content.Shared.CCVar;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.Corvax.TTS;
using Content.Shared.GameTicking;
using Content.Server._Lua.Language; // Lua
using Content.Shared.Players.RateLimiting;
using Content.Shared.Radio;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Content.Server.Corvax.TTS;

// ReSharper disable once InconsistentNaming
public sealed partial class TTSSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly TTSManager _ttsManager = default!;
    [Dependency] private readonly SharedTransformSystem _xforms = default!;
    [Dependency] private readonly IRobustRandom _rng = default!;
    [Dependency] private readonly LanguageSystem _language = default!; // Lua

    private readonly List<string> _sampleText =
        new()
        {
            "Съешь же ещё этих мягких французских булок, да выпей чаю.",
            "Клоун, прекрати разбрасывать банановые кожурки офицерам под ноги!",
            "Капитан, вы уверены что хотите назначить клоуна на должность главы персонала?",
            "Эс Бэ! Тут человек в сером костюме, с тулбоксом и в маске! Помогите!!",
            "Я надеюсь что инженеры внимательно следят за сингулярностью...",
            "Вы слышали эти странные крики в техах? Мне кажется туда ходить небезопасно.",
            "Вы не видели Гамлета? Мне кажется он забегал к вам на кухню.",
            "Здесь есть доктор? Человек умирает от отравленного пончика! Нужна помощь!",
            "Возле эвакуационного шаттла разгерметизация! Инженеры, нам срочно нужна ваша помощь!",
            "Бармен, налей мне самого крепкого вина, которое есть в твоих запасах!"
        };

    private const int MaxMessageChars = 100 * 3; // same as SingleBubbleCharLimit * 3
    private bool _isEnabled = false;

    public override void Initialize()
    {
        _cfg.OnValueChanged(CCCVars.TTSEnabled, v => _isEnabled = v, true);

        SubscribeLocalEvent<TransformSpeechEvent>(OnTransformSpeech);
        SubscribeLocalEvent<TTSComponent, EntitySpokeLanguageEvent>(OnEntitySpokeLanguage);
        SubscribeLocalEvent<TTSComponent, EntitySpokeToEntityEvent>(OnEntitySpokeToEntity);
        SubscribeLocalEvent<RadioSpokeEvent>(OnRadioSpokeEvent);
        SubscribeLocalEvent<AnnounceSpokeEvent>(OnAnnounceSpokeEvent);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeNetworkEvent<RequestPreviewTTSEvent>(OnRequestPreviewTTS);

        RegisterRateLimits();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _ttsManager.ResetCache();
    }

    private async void OnRequestPreviewTTS(RequestPreviewTTSEvent ev, EntitySessionEventArgs args)
    {
        if (!_isEnabled ||
            !_prototypeManager.TryIndex<TTSVoicePrototype>(ev.VoiceId, out var protoVoice))
            return;

        if (HandleRateLimit(args.SenderSession) != RateLimitStatus.Allowed)
            return;

        var previewText = _rng.Pick(_sampleText);
        var soundData = await GenerateTTS(previewText, protoVoice.Speaker);
        if (soundData is null)
            return;

        RaiseNetworkEvent(new PlayTTSEvent(soundData), Filter.SinglePlayer(args.SenderSession));
    }

    private async void OnEntitySpokeLanguage(EntityUid uid, TTSComponent component, EntitySpokeLanguageEvent args)
    {
        var voiceId = component.VoicePrototypeId;
        if (!_isEnabled || !component.Enabled ||
            args.Message.Length > MaxMessageChars ||
            voiceId == null)
            return;

        var voiceEv = new TransformSpeakerVoiceEvent(uid, voiceId);
        RaiseLocalEvent(uid, voiceEv);
        voiceId = voiceEv.VoiceId;

        if (!_prototypeManager.TryIndex<TTSVoicePrototype>(voiceId, out var protoVoice))
            return;

        if (args.IsWhisper)
        {
            HandleWhisper(uid, args.Message, args.ObfuscatedMessage, protoVoice.Speaker, args.OrgMsg, args.ObsMsg, args.LangMessage, args.ObfuscatedLangMessage);
            return;
        }

        if (args.OrgMsg.Count > 0 || args.ObsMsg.Count > 0)
        {
            if (args.OrgMsg.Count > 0) HandleSayToFilter(args.OrgMsg, uid, args.Message, protoVoice.Speaker);
            if (args.ObsMsg.Count > 0)
            {
                var obsText = args.LangMessage ?? args.ObfuscatedMessage;
                if (obsText != args.Message) HandleSayToFilter(args.ObsMsg, uid, obsText, protoVoice.Speaker);
            }
            return;
        }

        HandleSay(uid, args.Message, protoVoice.Speaker);
    }

    private async void OnEntitySpokeToEntity(EntityUid uid, TTSComponent component, EntitySpokeToEntityEvent args)
    {
        var voiceId = component.VoicePrototypeId;
        if (!_isEnabled || !component.Enabled ||
            args.Message.Length > MaxMessageChars ||
            voiceId == null)
            return;

        var voiceEv = new TransformSpeakerVoiceEvent(uid, voiceId);
        RaiseLocalEvent(uid, voiceEv);
        voiceId = voiceEv.VoiceId;

        if (!_prototypeManager.TryIndex<TTSVoicePrototype>(voiceId, out var protoVoice))
            return;

        HandleDirectSay(args.Target, args.Message, protoVoice.Speaker);
    }

    private async void OnRadioSpokeEvent(RadioSpokeEvent args)
    {
        if (!_isEnabled ||
            args.Message.Length > MaxMessageChars)
            return;

        if (!TryComp(args.Source, out TTSComponent? component) || !component.Enabled)
            return;

        var voiceId = component.VoicePrototypeId;

        if (voiceId == null)
            return;

        var voiceEv = new TransformSpeakerVoiceEvent(args.Source, voiceId);
        RaiseLocalEvent(args.Source, voiceEv);
        voiceId = voiceEv.VoiceId;

        if (!_prototypeManager.TryIndex<TTSVoicePrototype>(voiceId, out var protoVoice))
            return;

        var lang = _language.GetLanguage(args.Source);
        var obf = _language.ObfuscateSpeech(args.Message, lang);
        foreach (var device in args.Receivers)
        {
            var recipients = Filter.Pvs(device).Recipients;
            if (!recipients.Any()) continue;
            var org = new List<ICommonSession>();
            var obs = new List<ICommonSession>();
            foreach (var session in recipients)
            {
                if (session.AttachedEntity is not { Valid: true } listener) continue;
                if (_language.CanUnderstand(listener, lang.ID)) org.Add(session);
                else obs.Add(session);
            }
            if (org.Count > 0)
            {
                var soundData = await GenerateTTS(args.Message, protoVoice.Speaker);
                if (soundData is not null) RaiseNetworkEvent(new PlayTTSEvent(soundData, GetNetEntity(device), isRadio: true), Filter.Empty().AddPlayers(org));
            }

            if (obs.Count > 0)
            {
                var obsData = await GenerateTTS(obf, protoVoice.Speaker);
                if (obsData is not null) RaiseNetworkEvent(new PlayTTSEvent(obsData, GetNetEntity(device), isRadio: true), Filter.Empty().AddPlayers(obs));
            }
        }
    }

    private async void OnAnnounceSpokeEvent(AnnounceSpokeEvent args)
    {
        var voiceId = args.Voice;
        if (!_isEnabled ||
            args.Message.Length > _cfg.GetCVar(CCVars.ChatMaxAnnouncementLength) ||
            voiceId == null)
            return;

        if (args.Source != null)
        {
            var voiceEv = new TransformSpeakerVoiceEvent(args.Source.Value, voiceId);
            RaiseLocalEvent(args.Source.Value, voiceEv);
            voiceId = voiceEv.VoiceId;
        }

        if (!_prototypeManager.TryIndex<TTSVoicePrototype>(voiceId, out var protoVoice))
            return;

        Robust.Shared.Timing.Timer.Spawn(6000, () => HandleAnnounce(args.Message, protoVoice.Speaker)); // Awful, but better than sending announce sound to client in resource file
    }

    private async void HandleSay(EntityUid uid, string message, string speaker)
    {
        var soundData = await GenerateTTS(message, speaker);
        if (soundData is null) return;
        RaiseNetworkEvent(new PlayTTSEvent(soundData, GetNetEntity(uid)), Filter.Pvs(uid));
    }

    private async void HandleSayToFilter(Filter filter, EntityUid uid, string message, string speaker)
    {
        var soundData = await GenerateTTS(message, speaker);
        if (soundData is null) return;
        RaiseNetworkEvent(new PlayTTSEvent(soundData, GetNetEntity(uid)), filter);
    }

    private async void HandleDirectSay(EntityUid uid, string message, string speaker)
    {
        var soundData = await GenerateTTS(message, speaker);
        if (soundData is null) return;
        RaiseNetworkEvent(new PlayTTSEvent(soundData, GetNetEntity(uid)), uid);
    }

    private async void HandleRadio(EntityUid[] uids, string message, string speaker)
    {
        var soundData = await GenerateTTS(message, speaker);
        if (soundData is null) return;

        foreach (var uid in uids)
            RaiseNetworkEvent(new PlayTTSEvent(soundData, GetNetEntity(uid), isRadio: true), Filter.Entities(uid));
    }

    private async void HandleAnnounce(string message, string speaker)
    {
        var soundData = await GenerateTTS(message, speaker);
        if (soundData is null) return;
        RaiseNetworkEvent(new PlayTTSEvent(soundData), Filter.Broadcast());
    }

    private async void HandleWhisper(EntityUid uid, string message, string obfMessage, string speaker, Filter orgFilter, Filter obsFilter, string? langMessage = null, string? obfLangMessage = null)
    {
        var netEntity = GetNetEntity(uid);
        var cache = new Dictionary<string, byte[]>();
        async Task<byte[]?> GetAudio(string text)
        {
            if (cache.TryGetValue(text, out var d)) return d;
            var data = await GenerateTTS(text, speaker, true);
            if (data != null) cache[text] = data; return data;
        }

        var xformQuery = GetEntityQuery<TransformComponent>();
        var sourcePos = _xforms.GetWorldPosition(xformQuery.GetComponent(uid), xformQuery);
        var receptions = Filter.Pvs(uid).Recipients;
        var orgSet = new HashSet<ICommonSession>(orgFilter.Recipients);
        var obsSet = new HashSet<ICommonSession>(obsFilter.Recipients);
        foreach (var session in receptions)
        {
            if (!session.AttachedEntity.HasValue) continue;
            var xform = xformQuery.GetComponent(session.AttachedEntity.Value);
            var distance = (sourcePos - _xforms.GetWorldPosition(xform, xformQuery)).Length();
            if (distance > ChatSystem.WhisperMuffledRange)
                continue;

            var understands = orgSet.Contains(session);
            string textToSpeak;
            if (distance <= ChatSystem.WhisperClearRange)
            { textToSpeak = understands ? message : (langMessage ?? obfMessage); }
            else
            { textToSpeak = understands ? obfMessage : (obfLangMessage ?? obfMessage); }
            var data = await GetAudio(textToSpeak);
            if (data == null) continue;
            RaiseNetworkEvent(new PlayTTSEvent(data, netEntity, true), session);
        }
    }

    // ReSharper disable once InconsistentNaming
    private readonly Dictionary<string, Task<byte[]?>> _ttsTasks = new();
    private readonly SemaphoreSlim _ttsLock = new(1, 1);
    private async Task<byte[]?> GenerateTTS(string text, string speaker, bool isWhisper = false)
    {
        var textSanitized = Sanitize(text);
        if (textSanitized == "") return null;
        if (char.IsLetter(textSanitized[^1]))
            textSanitized += ".";

        var ssmlTraits = SoundTraits.RateFast;
        if (isWhisper)
            ssmlTraits = SoundTraits.PitchVerylow;
        var textSsml = ToSsmlText(textSanitized, ssmlTraits);

        var taskKey = $"{textSanitized}_{speaker}_{isWhisper}";
        await _ttsLock.WaitAsync();
        try
        {
            if (_ttsTasks.TryGetValue(taskKey, out var existing)) return await existing;
            var newTask = _ttsManager.ConvertTextToSpeech(speaker, textSsml);
            _ttsTasks[taskKey] = newTask;
        }
        finally
        { _ttsLock.Release(); }
        try
        { return await _ttsTasks[taskKey];  }
        finally
        {
            await _ttsLock.WaitAsync();
            try { _ttsTasks.Remove(taskKey); }
            finally { _ttsLock.Release(); }
        }
    }
    public sealed class EntitySpokeLanguageEvent : EntityEventArgs
    {
        public readonly string? ObfuscatedLangMessage;
        public readonly string? LangMessage;
        public readonly bool IsWhisper;
        public readonly Filter OrgMsg;
        public readonly Filter ObsMsg;
        public readonly EntityUid Source;
        public readonly string Message;
        public readonly string OriginalMessage;
        public readonly string ObfuscatedMessage;
        public RadioChannelPrototype? Channel;
        public EntitySpokeLanguageEvent( Filter orgMsg, Filter obsMsg, EntityUid source, string message, string originalMessage, RadioChannelPrototype? channel, bool isWhisper, string obfuscatedMessage, string? langMessage = null, string? obfuscatedLangMessage = null)
        {
            ObfuscatedLangMessage = obfuscatedLangMessage;
            LangMessage = langMessage;
            IsWhisper = isWhisper;
            OrgMsg = orgMsg;
            ObsMsg = obsMsg;
            Source = source;
            Message = message;
            OriginalMessage = originalMessage; // Corvax-TTS: Spec symbol sanitize
            Channel = channel;
            ObfuscatedMessage = obfuscatedMessage;
        }
    }
}
