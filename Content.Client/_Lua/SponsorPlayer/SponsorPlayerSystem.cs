// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Lua.SponsorPlayer;
using Content.Shared.CCVar;
using Robust.Client.Audio;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;
using System.Linq;
namespace Content.Client._Lua.SponsorPlayer;

public sealed class SponsorPlayerSystem : EntitySystem
{
    [Dependency] private readonly IResourceManager _res = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    private readonly MemoryContentRoot _contentRoot = new();
    private static readonly ResPath Prefix = ResPath.Root / "SponsorMusic";
    private readonly Dictionary<EntityUid, ActivePlayback> _activeStreams = new();
    private float _jukeboxVolume = 1.0f;
    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            UpdateActiveStreamVolumes();
        }
    }

    private float _volume = 1.0f;
    public override void Initialize()
    {
        base.Initialize();
        _res.AddRoot(Prefix, _contentRoot);
        _cfg.OnValueChanged(CCVars.JukeboxVolume, OnJukeboxVolumeChanged, true);
        SubscribeNetworkEvent<CacheSponsorMusicEvent>(OnCacheSponsorMusic);
        SubscribeNetworkEvent<PlaySponsorMusicEvent>(OnPlaySponsorMusic);
        SubscribeNetworkEvent<StopSponsorMusicEvent>(OnStopSponsorMusic);
        SubscribeLocalEvent<SponsorPlayerComponent, AfterAutoHandleStateEvent>(OnAfterState);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        foreach (var (sourceUid, playback) in _activeStreams.ToArray())
        {
            if (!Exists(sourceUid))
            {
                StopActivePlayback(sourceUid);
                continue;
            }
            if (!TryComp<AudioComponent>(playback.StreamUid, out var audioComp))
            {
                RaiseNetworkEvent(new SponsorTrackFinishedEvent(GetNetEntity(sourceUid), playback.Hash));
                _activeStreams.Remove(sourceUid);
                continue;
            }
            if (audioComp.State != AudioState.Stopped) continue;
            RaiseNetworkEvent(new SponsorTrackFinishedEvent(GetNetEntity(sourceUid), playback.Hash));
            _activeStreams.Remove(sourceUid);
        }
    }

    public override void Shutdown()
    {
        base.Shutdown();
        foreach (var (sourceUid, _) in _activeStreams.ToArray())
        { StopActivePlayback(sourceUid); }
        _activeStreams.Clear();
        _contentRoot.Dispose();
    }

    private void OnCacheSponsorMusic(CacheSponsorMusicEvent ev)
    {
        var filePath = GetCachedTrackPath(ev.Hash);
        _contentRoot.AddOrUpdateFile(filePath, ev.Data);
    }

    private void OnPlaySponsorMusic(PlaySponsorMusicEvent ev)
    {
        var filePath = GetCachedTrackPath(ev.Hash);
        if (!_contentRoot.FileExists(filePath))
        {
            Logger.Warning($"Sponsor track '{ev.Hash}' is not cached on client.");
            return;
        }

        var audioResource = new AudioResource();
        audioResource.Load(IoCManager.Instance!, Prefix / filePath);
        var audioParams = AudioParams.Default.WithMaxDistance(7f).WithRolloffFactor(1f).WithVolume(SharedAudioSystem.GainToVolume(GetEffectiveVolume()));
        var soundSpecifier = new ResolvedPathSpecifier(Prefix / filePath);
        var sourceUid = GetEntity(ev.SourceUid);
        StopActivePlayback(sourceUid);
        var stream = _audio.PlayEntity(audioResource.AudioStream, sourceUid, soundSpecifier, audioParams);
        if (stream != null) _activeStreams[sourceUid] = new ActivePlayback(stream.Value.Entity, ev.Hash);
    }

    private void OnStopSponsorMusic(StopSponsorMusicEvent ev)
    {
        var sourceUid = GetEntity(ev.SourceUid);
        StopActivePlayback(sourceUid);
    }

    private void OnAfterState(Entity<SponsorPlayerComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!_uiSystem.TryGetOpenUi<SponsorPlayerBoundUserInterface>(ent.Owner, SponsorPlayerUiKey.Key, out var bui)) return;
        bui.Reload();
    }

    private static ResPath GetCachedTrackPath(string hash)
    { return new ResPath($"{hash}.ogg"); }

    private void StopActivePlayback(EntityUid sourceUid)
    {
        if (!_activeStreams.TryGetValue(sourceUid, out var playback)) return;
        _audio.Stop(playback.StreamUid);
        _activeStreams.Remove(sourceUid);
    }

    private void UpdateActiveStreamVolumes()
    {
        foreach (var (sourceUid, playback) in _activeStreams.ToArray())
        {
            if (!TryComp<AudioComponent>(playback.StreamUid, out var audioComp))
            {
                _activeStreams.Remove(sourceUid);
                continue;
            }
            _audio.SetGain(playback.StreamUid, GetEffectiveVolume(), audioComp);
        }
    }

    private void OnJukeboxVolumeChanged(float value)
    {
        _jukeboxVolume = Math.Clamp(value, 0f, 1f);
        UpdateActiveStreamVolumes();
    }

    private float GetEffectiveVolume()
    { return Volume * _jukeboxVolume; }
    private sealed record ActivePlayback(EntityUid StreamUid, string Hash);
}
