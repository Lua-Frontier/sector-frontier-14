// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Lua.SponsorPlayer;
using Content.Shared.Hands;
using Content.Shared.Interaction.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Content.Server._Lua.SponsorPlayer;

public sealed class SponsorPlayerSystem : EntitySystem
{
    [Dependency] private readonly SponsorMusicManager _musicManager = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    private ISawmill _sawmill = default!;
    private readonly Dictionary<ICommonSession, HashSet<string>> _knownTrackHashes = new();
    private readonly ConcurrentQueue<(EntityUid Uid, List<SponsorTrackInfo>? Tracks)> _pendingTracks = new();
    private readonly ConcurrentQueue<PendingAudioRequest> _pendingAudio = new();

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("sponsor_music");
        SubscribeLocalEvent<SponsorPlayerComponent, GotEquippedHandEvent>(OnPickedUp);
        SubscribeLocalEvent<SponsorPlayerComponent, GotUnequippedHandEvent>(OnDropped);
        SubscribeLocalEvent<SponsorPlayerComponent, DroppedEvent>(OnDropped);
        SubscribeLocalEvent<SponsorPlayerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SponsorPlayerComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeNetworkEvent<SponsorTrackFinishedEvent>(OnTrackFinished);
        Subs.BuiEvents<SponsorPlayerComponent>(SponsorPlayerUiKey.Key, subs =>
        {
            subs.Event<SponsorPlayerRequestTracksMessage>(OnRequestTracks);
            subs.Event<SponsorPlayerPlayTrackMessage>(OnPlayTrack);
            subs.Event<SponsorPlayerPreviousMessage>(OnPreviousTrack);
            subs.Event<SponsorPlayerNextMessage>(OnNextTrack);
            subs.Event<SponsorPlayerRepeatMessage>(OnRepeatToggled);
            subs.Event<SponsorPlayerStopMessage>(OnStop);
        });
    }

    public override void Update(float frameTime)
    {
        while (_pendingTracks.TryDequeue(out var item))
        {
            if (!Exists(item.Uid)) continue;
            var state = item.Tracks != null ? new SponsorPlayerBoundUserInterfaceState(item.Tracks) : new SponsorPlayerBoundUserInterfaceState(new(), "sponsor-player-api-error");
            _uiSystem.SetUiState(item.Uid, SponsorPlayerUiKey.Key, state);
        }

        while (_pendingAudio.TryDequeue(out var audio))
        {
            if (!Exists(audio.Uid) || audio.Data == null) continue;
            var track = EnsureTrackHash(audio.Track, audio.Data);
            if (TryComp<SponsorPlayerComponent>(audio.Uid, out var comp))
            {
                comp.AudioStream = _audio.Stop(comp.AudioStream);
                comp.CurrentTrackId = track.Id;
                comp.CurrentTrackHash = track.Hash;
                comp.CurrentTrackTitle = track.Title;
                comp.PlaybackHistory = audio.History;
                comp.PlaybackHistoryIndex = audio.HistoryIndex;
                comp.IsPlaying = true;
                Dirty(audio.Uid, comp);
            }
            var netEnt = GetNetEntity(audio.Uid);
            var filter = Filter.Pvs(audio.Uid, audio.Range);
            foreach (var session in filter.Recipients)
            {
                if (!EnsureSessionHashCache(session, track.Hash)) continue;
                RaiseNetworkEvent(new CacheSponsorMusicEvent(track.Hash, audio.Data), session);
            }
            RaiseNetworkEvent(new PlaySponsorMusicEvent(track.Hash, netEnt), filter);
        }
    }

    private void OnPickedUp(Entity<SponsorPlayerComponent> ent, ref GotEquippedHandEvent args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor)) return;
        if (ent.Comp.OwnerUserId != null) return;
        ent.Comp.OwnerUserId = actor.PlayerSession.UserId.UserId.ToString();
        Dirty(ent);
    }

    private void OnDropped(Entity<SponsorPlayerComponent> ent, ref GotUnequippedHandEvent args)
    {
        StopPlayback(ent.Owner, ent.Comp);
        ent.Comp.CurrentTrackTitle = null;
        ent.Comp.CurrentTrackId = null;
        ent.Comp.CurrentTrackHash = null;
        ent.Comp.PlaybackHistory.Clear();
        ent.Comp.PlaybackHistoryIndex = -1;
        Dirty(ent);
    }

    private void OnDropped(Entity<SponsorPlayerComponent> ent, ref DroppedEvent args)
    {
        StopPlayback(ent.Owner, ent.Comp);
        ent.Comp.CurrentTrackTitle = null;
        ent.Comp.CurrentTrackId = null;
        ent.Comp.CurrentTrackHash = null;
        ent.Comp.PlaybackHistory.Clear();
        ent.Comp.PlaybackHistoryIndex = -1;
        Dirty(ent);
    }

    private void OnShutdown(Entity<SponsorPlayerComponent> ent, ref ComponentShutdown args)
    { StopPlayback(ent.Owner, ent.Comp); }

    private void OnPlayerDetached(PlayerDetachedEvent ev)
    { if (ev.Player.Status == SessionStatus.Disconnected) _knownTrackHashes.Remove(ev.Player); }

    private void OnUiOpened(Entity<SponsorPlayerComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (ent.Comp.PlaybackMode == SponsorPlayerPlaybackMode.Shuffle)
        {
            ent.Comp.PlaybackMode = SponsorPlayerPlaybackMode.Single;
            Dirty(ent);
        }
        OnRequestTracks(ent.Owner, ent.Comp, new SponsorPlayerRequestTracksMessage());
    }

    private void OnTrackFinished(SponsorTrackFinishedEvent msg, EntitySessionEventArgs args)
    {
        var uid = GetEntity(msg.SourceUid);
        if (!TryComp<SponsorPlayerComponent>(uid, out var comp)) return;
        if (comp.OwnerUserId == null || !string.Equals(comp.OwnerUserId, args.SenderSession.UserId.UserId.ToString(), StringComparison.Ordinal))
        { return; }
        if (!comp.IsPlaying || string.IsNullOrWhiteSpace(comp.CurrentTrackHash) || !string.Equals(comp.CurrentTrackHash, msg.Hash, StringComparison.OrdinalIgnoreCase))
        { return; }
        comp.AudioStream = _audio.Stop(comp.AudioStream);
        comp.IsPlaying = false;
        Dirty(uid, comp);
        RequestRelativeTrack(uid, comp, RelativeTrackDirection.Next, automaticAdvance: true);
    }

    private void OnRequestTracks(EntityUid uid, SponsorPlayerComponent comp, SponsorPlayerRequestTracksMessage msg)
    {
        if (comp.OwnerUserId == null)
        {
            _uiSystem.SetUiState(uid, SponsorPlayerUiKey.Key, new SponsorPlayerBoundUserInterfaceState(new(), "sponsor-player-not-sponsor"));
            return;
        }
        var userId = comp.OwnerUserId;
        var capturedUid = uid;
        _ = Task.Run(async () =>
        {
            try
            {
                var tracks = await _musicManager.FetchTrackList(userId);
                _pendingTracks.Enqueue((capturedUid, tracks));
            }
            catch (Exception ex)
            {
                _sawmill.Error($"SponsorPlayer: FetchTrackList exception: {ex.Message}");
                _pendingTracks.Enqueue((capturedUid, null));
            }
        });
    }

    private void OnPlayTrack(EntityUid uid, SponsorPlayerComponent comp, SponsorPlayerPlayTrackMessage msg)
    {
        if (comp.OwnerUserId == null) return;
        if (comp.IsPlaying)
        {
            StopPlayback(uid, comp);
            Dirty(uid, comp);
        }
        var userId = comp.OwnerUserId;
        var range = comp.Range;
        var capturedUid = uid;
        var trackHash = msg.TrackHash ?? string.Empty;
        var track = new SponsorTrackInfo(msg.TrackId, msg.TrackTitle ?? msg.TrackId, trackHash, 0);
        var history = BuildDirectHistory(comp.PlaybackHistory, comp.PlaybackHistoryIndex, msg.TrackId);
        _ = Task.Run(async () =>
        {
            var data = await _musicManager.FetchTrack(userId, msg.TrackId, trackHash);
            _pendingAudio.Enqueue(new PendingAudioRequest(capturedUid, data, range, track, history.History, history.Index));
        });
    }

    private void OnPreviousTrack(EntityUid uid, SponsorPlayerComponent comp, SponsorPlayerPreviousMessage msg)
    { RequestRelativeTrack(uid, comp, RelativeTrackDirection.Previous); }
    private void OnNextTrack(EntityUid uid, SponsorPlayerComponent comp, SponsorPlayerNextMessage msg)
    { RequestRelativeTrack(uid, comp, RelativeTrackDirection.Next); }
    private void OnRepeatToggled(EntityUid uid, SponsorPlayerComponent comp, SponsorPlayerRepeatMessage msg)
    {
        comp.PlaybackMode = msg.Enabled ? SponsorPlayerPlaybackMode.Repeat : SponsorPlayerPlaybackMode.Single;
        Dirty(uid, comp);
    }
    private void OnStop(EntityUid uid, SponsorPlayerComponent comp, SponsorPlayerStopMessage msg)
    {
        StopPlayback(uid, comp);
        Dirty(uid, comp);
    }

    private void RequestRelativeTrack(EntityUid uid, SponsorPlayerComponent comp, RelativeTrackDirection direction, bool automaticAdvance = false)
    {
        if (comp.OwnerUserId == null) return;
        if (comp.IsPlaying)
        {
            StopPlayback(uid, comp);
            Dirty(uid, comp);
        }
        var userId = comp.OwnerUserId;
        var currentTrackId = comp.CurrentTrackId;
        var playbackMode = comp.PlaybackMode;
        var range = comp.Range;
        var capturedUid = uid;
        var historySnapshot = comp.PlaybackHistory.ToList();
        var historyIndex = comp.PlaybackHistoryIndex;
        _ = Task.Run(async () =>
        {
            var tracks = await _musicManager.FetchTrackList(userId);
            if (tracks == null)
            {
                _pendingTracks.Enqueue((capturedUid, null));
                return;
            }
            if (tracks.Count == 0)
            {
                _pendingTracks.Enqueue((capturedUid, tracks));
                return;
            }
            var resolved = ResolveTrackForNavigation(direction, tracks, currentTrackId, playbackMode, historySnapshot, historyIndex, automaticAdvance);
            var data = await _musicManager.FetchTrack(userId, resolved.Track.Id, resolved.Track.Hash);
            _pendingAudio.Enqueue(new PendingAudioRequest(capturedUid, data, range, resolved.Track, resolved.History, resolved.HistoryIndex));
        });
    }

    private static NavigationResult ResolveTrackForNavigation(RelativeTrackDirection direction, List<SponsorTrackInfo> tracks, string? currentTrackId, SponsorPlayerPlaybackMode playbackMode, List<string> history, int historyIndex, bool automaticAdvance)
    {
        if (tracks.Count == 0) throw new InvalidOperationException("Track navigation requires at least one track.");
        if (automaticAdvance && playbackMode == SponsorPlayerPlaybackMode.Repeat)
        {
            var repeated = FindTrackById(tracks, currentTrackId) ?? tracks[0];
            var repeatHistory = BuildDirectHistory(history, historyIndex, repeated.Id);
            return new NavigationResult(repeated, repeatHistory.History, repeatHistory.Index);
        }
        if (playbackMode == SponsorPlayerPlaybackMode.Shuffle) return ResolveShuffleNavigation(direction, tracks, currentTrackId, history, historyIndex);
        var currentIndex = tracks.FindIndex(t => t.Id == currentTrackId);
        if (currentIndex == -1) currentIndex = direction == RelativeTrackDirection.Next ? -1 : 0;
        var nextIndex = direction == RelativeTrackDirection.Next ? (currentIndex + 1 + tracks.Count) % tracks.Count : (currentIndex - 1 + tracks.Count) % tracks.Count;
        var track = tracks[nextIndex];
        var directHistory = BuildDirectHistory(history, historyIndex, track.Id);
        return new NavigationResult(track, directHistory.History, directHistory.Index);
    }

    private static NavigationResult ResolveShuffleNavigation(RelativeTrackDirection direction, List<SponsorTrackInfo> tracks, string? currentTrackId, List<string> history, int historyIndex)
    {
        if (direction == RelativeTrackDirection.Previous && historyIndex > 0)
        {
            var previousIndex = historyIndex - 1;
            var previousTrack = FindTrackById(tracks, history[previousIndex]) ?? tracks[0];
            return new NavigationResult(previousTrack, history, previousIndex);
        }
        if (direction == RelativeTrackDirection.Next && historyIndex >= 0 && historyIndex < history.Count - 1)
        {
            var nextIndex = historyIndex + 1;
            var nextTrack = FindTrackById(tracks, history[nextIndex]) ?? tracks[0];
            return new NavigationResult(nextTrack, history, nextIndex);
        }
        var pool = tracks.Where(t => tracks.Count == 1 || t.Id != currentTrackId).ToList();
        var chosen = pool[Random.Shared.Next(pool.Count)];
        var newHistory = history.ToList();
        var newIndex = historyIndex;
        if (newIndex < 0 && currentTrackId != null)
        {
            newHistory.Add(currentTrackId);
            newIndex = 0;
        }
        if (newIndex >= 0 && newIndex < newHistory.Count - 1) newHistory.RemoveRange(newIndex + 1, newHistory.Count - newIndex - 1);
        if (newHistory.Count == 0 || newHistory[^1] != chosen.Id) newHistory.Add(chosen.Id);
        newIndex = newHistory.Count - 1;
        if (newHistory.Count > 25)
        {
            newHistory.RemoveRange(0, newHistory.Count - 25);
            newIndex = newHistory.Count - 1;
        }
        return new NavigationResult(chosen, newHistory, newIndex);
    }

    private static SponsorTrackInfo? FindTrackById(List<SponsorTrackInfo> tracks, string? trackId)
    {
        if (trackId == null) return null;
        return tracks.FirstOrDefault(t => t.Id == trackId);
    }

    private static HistoryState BuildDirectHistory(List<string> history, int historyIndex, string trackId)
    {
        var newHistory = history.ToList();
        var newIndex = historyIndex;
        if (newIndex >= 0 && newIndex < newHistory.Count - 1) newHistory.RemoveRange(newIndex + 1, newHistory.Count - newIndex - 1);
        if (newHistory.Count == 0 || newHistory[^1] != trackId) newHistory.Add(trackId);
        newIndex = newHistory.Count - 1;
        if (newHistory.Count > 25)
        {
            newHistory.RemoveRange(0, newHistory.Count - 25);
            newIndex = newHistory.Count - 1;
        }
        return new HistoryState(newHistory, newIndex);
    }
    private sealed record PendingAudioRequest(EntityUid Uid, byte[]? Data, float Range, SponsorTrackInfo Track, List<string> History, int HistoryIndex);
    private sealed record NavigationResult(SponsorTrackInfo Track, List<string> History, int HistoryIndex);
    private sealed record HistoryState(List<string> History, int Index);
    private enum RelativeTrackDirection : byte
    {
        Previous,
        Next,
    }

    private void StopPlayback(EntityUid uid, SponsorPlayerComponent comp)
    {
        comp.AudioStream = _audio.Stop(comp.AudioStream);
        comp.IsPlaying = false;
        var filter = Filter.Pvs(uid, comp.Range);
        RaiseNetworkEvent(new StopSponsorMusicEvent(GetNetEntity(uid)), filter);
    }

    private bool EnsureSessionHashCache(ICommonSession session, string hash)
    {
        if (string.IsNullOrWhiteSpace(hash)) return true;
        if (!_knownTrackHashes.TryGetValue(session, out var hashes))
        {
            hashes = new HashSet<string>(StringComparer.Ordinal);
            _knownTrackHashes[session] = hashes;
        }
        return hashes.Add(hash);
    }

    private static SponsorTrackInfo EnsureTrackHash(SponsorTrackInfo track, byte[] data)
    {
        if (!string.IsNullOrWhiteSpace(track.Hash)) return track;
        var hash = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
        return new SponsorTrackInfo(track.Id, track.Title, hash, track.DurationSeconds);
    }
}
