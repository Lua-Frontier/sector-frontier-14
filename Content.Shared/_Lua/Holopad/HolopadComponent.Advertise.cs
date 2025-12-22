// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Shared.Humanoid;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Shared.Holopad;

public sealed partial class HolopadComponent
{
    [DataField("scriptedMessages")]
    public List<HolopadScriptedMessageStep> ScriptedMessages = new();

    [DataField("scriptedVoiceId")]
    public string? ScriptedVoiceId;

    [DataField("scriptedSenderName")]
    public string? ScriptedSenderName;

    [DataField("scriptedAvatarProto")]
    public EntProtoId? ScriptedAvatarProtoId;

    [DataField("scriptedAvatarAppearance")]
    public HolopadAvatarAppearanceSettings? ScriptedAvatarAppearance;

    [DataField("scriptedAvatarOutfit")]
    public string? ScriptedAvatarOutfitId;

    [DataField("scriptedStartDelaySeconds")]
    public float ScriptedStartDelaySeconds { get; private set; } = 0.5f;

    [DataField("scriptedEndDelaySeconds")]
    public float ScriptedEndDelaySeconds { get; private set; } = 2.0f;

    [DataField("scriptedBroadcastToSector")]
    public bool ScriptedBroadcastToSector { get; private set; } = false;
}

[DataRecord]
public partial record struct HolopadScriptedMessageStep()
{
    [DataField("message")]
    public string Message { get; set; } = string.Empty;

    [DataField("delaySeconds")]
    public float DelaySeconds { get; set; } = 0f;

    [DataField("musicPath")]
    public string? MusicPath { get; set; }

    [DataField("musicVolumeDb")]
    public float MusicVolumeDb { get; set; } = -4f;

    [DataField("voiceId")]
    public string? VoiceId { get; set; }
}

[DataRecord]
public partial record struct HolopadAvatarMarkingSettings()
{
    [DataField("markingId", required: true)]
    public string MarkingId { get; set; } = string.Empty;

    [DataField("markingColor")]
    public List<Color> MarkingColors { get; set; } = new();
}

[DataRecord]
public partial record struct HolopadAvatarAppearanceSettings()
{
    [DataField("name")]
    public string Name { get; set; } = string.Empty;

    [DataField("voice")]
    public string Voice { get; set; } = string.Empty;

    [DataField("species")]
    public string Species { get; set; } = "Human";

    [DataField("sex")]
    public Sex Sex { get; set; } = Sex.Unsexed;

    [DataField("gender")]
    public Gender Gender { get; set; } = Gender.Epicene;

    [DataField("age")]
    public int Age { get; set; } = 20;

    [DataField("skinColor")]
    public Color SkinColor { get; set; } = Color.White;

    [DataField("eyeColor")]
    public Color EyeColor { get; set; } = Color.Black;

    [DataField("hairColor")]
    public Color HairColor { get; set; } = Color.Black;

    [DataField("facialHairColor")]
    public Color FacialHairColor { get; set; } = Color.Black;

    [DataField("hairStyleId")]
    public string HairStyleId { get; set; } = string.Empty;

    [DataField("facialHairStyleId")]
    public string FacialHairStyleId { get; set; } = string.Empty;

    [DataField("markings")]
    public List<HolopadAvatarMarkingSettings> Markings { get; set; } = new();
}

