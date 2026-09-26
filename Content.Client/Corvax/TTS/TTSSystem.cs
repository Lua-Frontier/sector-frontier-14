using System.IO;
using Content.Shared.Chat;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.Corvax.TTS;
using Robust.Client.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;

namespace Content.Client.Corvax.TTS;

/// <summary>
/// Plays TTS audio in world
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class TTSSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IAudioManager _audioManager = default!;
    [Dependency] private readonly AudioSystem _audio = default!;

    private ISawmill _sawmill = default!;

    /// <summary>
    /// Reducing the volume of the TTS when whispering. Will be converted to logarithm.
    /// </summary>
    private const float WhisperFade = 3f;

    /// <summary>
    /// The volume at which the TTS sound will not be heard.
    /// </summary>
    private const float MinimalVolume = -6f;

    private float _volume = 0.0f;
    private float _volumeRadio = 0.0f;
    private bool _playRadio = true;
    private bool _alive = true;

    public override void Initialize()
    {
        _sawmill = Logger.GetSawmill("tts");
        _alive = true;
        _cfg.OnValueChanged(CCCVars.TTSVolume, OnTtsVolumeChanged, true);
        _cfg.OnValueChanged(CCCVars.TTSVolumeRadio, OnTtsRadioVolumeChanged, true);
        _cfg.OnValueChanged(CCCVars.RadioTTSSoundsEnabled, OnTtsPlayRadioChanged, true);
        SubscribeNetworkEvent<PlayTTSEvent>(OnPlayTTS);
    }

    public override void Shutdown()
    {
        _alive = false;
        base.Shutdown();
        _cfg.UnsubValueChanged(CCCVars.TTSVolume, OnTtsVolumeChanged);
        _cfg.UnsubValueChanged(CCCVars.TTSVolumeRadio, OnTtsRadioVolumeChanged);
        _cfg.UnsubValueChanged(CCCVars.RadioTTSSoundsEnabled, OnTtsPlayRadioChanged);
    }

    public void RequestPreviewTTS(string voiceId)
    {
        RaiseNetworkEvent(new RequestPreviewTTSEvent(voiceId));
    }

    private void OnTtsVolumeChanged(float volume)
    {
        _volume = volume;
    }

    private void OnTtsRadioVolumeChanged(float volume)
    {
        _volumeRadio = volume;
    }

    private void OnTtsPlayRadioChanged(bool radio)
    {
        _playRadio = radio;
    }

    private void OnPlayTTS(PlayTTSEvent ev)
    {
        if (!_alive)
            return;

        if (ev.IsRadio && !_playRadio)
            return;

        if (ev.Data.Length == 0)
            return;

        _sawmill.Verbose($"Play TTS audio {ev.Data.Length} bytes from {ev.SourceUid} entity");

        AudioStream stream;
        try
        {
            stream = LoadStream(ev);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to load TTS audio ({ev.Data.Length} bytes, fmt={ev.AudioFormat}): {e.Message}");
            return;
        }

        var audioParams = AudioParams.Default
            .WithVolume(AdjustVolume(ev.IsWhisper, ev.IsRadio))
            .WithMaxDistance(AdjustDistance(ev.IsWhisper));

        if (ev.SourceUid != null)
        {
            var sourceUid = GetEntity(ev.SourceUid.Value);
            if (!sourceUid.IsValid() || Deleted(sourceUid))
                return;

            _audio.PlayEntity(stream, sourceUid, null, audioParams);
        }
        else
        {
            _audio.PlayGlobal(stream, null, audioParams);
        }
    }

    private AudioStream LoadStream(PlayTTSEvent ev)
    {
        using var memory = new MemoryStream(ev.Data, writable: false);
        var ext = ResolveExtension(ev);

        if (ext == "wav")
            return _audioManager.LoadAudioWav(memory, "tts");

        return _audioManager.LoadAudioOggVorbis(memory, "tts");
    }

    private static string ResolveExtension(PlayTTSEvent ev)
    {
        if (!string.IsNullOrWhiteSpace(ev.AudioFormat))
        {
            var fmt = ev.AudioFormat.TrimStart('.').ToLowerInvariant();
            if (fmt is "wav" or "ogg")
                return fmt;
        }

        if (ev.Data.Length >= 12 &&
            ev.Data[0] == (byte) 'R' &&
            ev.Data[1] == (byte) 'I' &&
            ev.Data[2] == (byte) 'F' &&
            ev.Data[3] == (byte) 'F')
            return "wav";

        return "ogg";
    }

    private float AdjustVolume(bool isWhisper, bool isRadio)
    {
        var volume = MinimalVolume + SharedAudioSystem.GainToVolume(_volume);

        if (isWhisper)
        {
            volume -= SharedAudioSystem.GainToVolume(WhisperFade);
        }

        if (isRadio)
        {
            volume = MinimalVolume + SharedAudioSystem.GainToVolume(_volumeRadio);
        }

        return volume;
    }

    private float AdjustDistance(bool isWhisper)
    {
        return isWhisper ? SharedChatSystem.WhisperMuffledRange : SharedChatSystem.VoiceRange;
    }
}
