// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

namespace Content.Shared._Lua.Announce;

public sealed class AnnouncementOverlayParams
{
    public const AnnouncementPreset PresetComms = AnnouncementPreset.Comms;
    public const AnnouncementPreset PresetFaction = AnnouncementPreset.Faction;
    public const AnnouncementPreset PresetAlert = AnnouncementPreset.Alert;
    public const AnnouncementPreset PresetOnboardComputer = AnnouncementPreset.OnboardComputer;

    public string Message { get; set; } = string.Empty;
    public string? SenderTitle { get; set; }
    public EntityUid? Speaker { get; set; }
    public EntityUid? Source { get; set; }
    public string? FactionId { get; set; }
    public Color? ColorOverride { get; set; }
    public AnnouncementPreset? Preset { get; set; }

    public AnnouncementPreset? ResolvePreset()
    {
        if (Preset is { } preset)
            return preset;
        if (Speaker != null)
            return PresetComms;
        if (!string.IsNullOrWhiteSpace(FactionId))
            return PresetFaction;
        return null;
    }
}
