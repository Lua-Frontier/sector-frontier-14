using System;
using Content.Shared._Lua.Announce;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Announce;

[Serializable, NetSerializable]
public sealed class AnnouncementNetData
{
    public string[] Text { get; set; } = Array.Empty<string>();
    public AnnouncementPreset AnnouncementId { get; set; }
    public float Priority { get; set; }
    public NetEntity? SpeakerEntity { get; set; }
    public string? SpeakerName { get; set; }
    public string? SpeakerJobTitle { get; set; }
    public uint OverrideId { get; set; }
    public string? TitleOverride { get; set; }
    public Color? TitleColorOverride { get; set; }
    public Color? TextColorOverride { get; set; }
    public string? DecalRsi { get; set; }
    public string? DecalState { get; set; }
}

[Serializable, NetSerializable]
public sealed class AnnouncementNetMessage : EntityEventArgs
{
    public AnnouncementNetData Data { get; }

    public AnnouncementNetMessage(AnnouncementNetData data)
    {
        Data = data;
    }
}

[Serializable, NetSerializable]
public sealed class AnnouncementPlaybackDoneMsg : EntityEventArgs
{
    public uint OverrideId { get; }

    public AnnouncementPlaybackDoneMsg(uint overrideId)
    {
        OverrideId = overrideId;
    }
}

