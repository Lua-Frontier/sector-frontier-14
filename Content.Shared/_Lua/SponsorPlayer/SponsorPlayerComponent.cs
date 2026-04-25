// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Lua.SponsorPlayer;

[Serializable, NetSerializable]
public sealed class SponsorPlayerBoundUserInterfaceState : BoundUserInterfaceState
{
    public List<SponsorTrackInfo> Tracks { get; set; } = new();
    public string? Error { get; set; }
    public SponsorPlayerBoundUserInterfaceState() { }

    public SponsorPlayerBoundUserInterfaceState(List<SponsorTrackInfo> tracks, string? error = null)
    {
        Tracks = tracks;
        Error = error;
    }
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class SponsorPlayerComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Range = 7f;

    [DataField, AutoNetworkedField]
    public string? OwnerUserId;

    [DataField, AutoNetworkedField]
    public string? CurrentTrackTitle;

    [DataField, AutoNetworkedField]
    public string? CurrentTrackId;

    [DataField, AutoNetworkedField]
    public string? CurrentTrackHash;

    [DataField, AutoNetworkedField]
    public SponsorPlayerPlaybackMode PlaybackMode = SponsorPlayerPlaybackMode.Single;

    [DataField, AutoNetworkedField]
    public bool IsPlaying;

    [ViewVariables]
    public EntityUid? AudioStream;

    [ViewVariables]
    public List<string> PlaybackHistory = new();

    [ViewVariables]
    public int PlaybackHistoryIndex = -1;
}

[Serializable, NetSerializable]
public enum SponsorPlayerUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class SponsorPlayerRequestTracksMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class SponsorPlayerPlayTrackMessage : BoundUserInterfaceMessage
{
    public string TrackId { get; set; } = "";
    public string? TrackTitle { get; set; }
    public string? TrackHash { get; set; }

    public SponsorPlayerPlayTrackMessage() { }

    public SponsorPlayerPlayTrackMessage(string trackId, string? trackTitle = null, string? trackHash = null)
    {
        TrackId = trackId;
        TrackTitle = trackTitle;
        TrackHash = trackHash;
    }
}

[Serializable, NetSerializable]
public sealed class SponsorPlayerStopMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class SponsorPlayerPreviousMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class SponsorPlayerNextMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class SponsorPlayerRepeatMessage(bool enabled) : BoundUserInterfaceMessage
{
    public bool Enabled { get; } = enabled;
}

[Serializable, NetSerializable]
public sealed class CacheSponsorMusicEvent : EntityEventArgs
{
    public string Hash { get; }
    public byte[] Data { get; }

    public CacheSponsorMusicEvent(string hash, byte[] data)
    {
        Hash = hash;
        Data = data;
    }
}

[Serializable, NetSerializable]
public sealed class PlaySponsorMusicEvent : EntityEventArgs
{
    public string Hash { get; }
    public NetEntity SourceUid { get; }

    public PlaySponsorMusicEvent(string hash, NetEntity sourceUid)
    {
        Hash = hash;
        SourceUid = sourceUid;
    }
}

[Serializable, NetSerializable]
public sealed class StopSponsorMusicEvent : EntityEventArgs
{
    public NetEntity SourceUid { get; }
    public StopSponsorMusicEvent(NetEntity sourceUid)
    { SourceUid = sourceUid; }
}

[Serializable, NetSerializable]
public sealed class SponsorTrackFinishedEvent : EntityEventArgs
{
    public NetEntity SourceUid { get; }
    public string Hash { get; }

    public SponsorTrackFinishedEvent(NetEntity sourceUid, string hash)
    {
        SourceUid = sourceUid;
        Hash = hash;
    }
}

[Serializable, NetSerializable]
public sealed class SponsorTrackInfo
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Hash { get; set; } = "";
    public int DurationSeconds { get; set; }
    public SponsorTrackInfo() { }
    public SponsorTrackInfo(string id, string title, string hash, int durationSeconds)
    {
        Id = id;
        Title = title;
        Hash = hash;
        DurationSeconds = durationSeconds;
    }
}

[Serializable, NetSerializable]
public enum SponsorPlayerPlaybackMode : byte
{
    Single,
    Shuffle,
    Repeat,
}
