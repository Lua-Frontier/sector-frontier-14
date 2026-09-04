// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using System.Collections.Generic;
using Robust.Shared.Utility;

namespace Content.Shared._Lua.Announce;

public readonly record struct AnnouncementPresetDefinition(
    AnnouncementPreset Preset,
    string Id,
    LocId Name,
    float Priority);

public static class AnnouncementPresetCatalog
{
    private static readonly AnnouncementPresetDefinition[] Definitions =
    {
        new(AnnouncementPreset.Comms, "FrontierComms", "lua-announcement-preset-comms", 9f),
        new(AnnouncementPreset.Faction, "FrontierFaction", "lua-announcement-preset-faction", 8f),
        new(AnnouncementPreset.Alert, "FrontierAlert", "lua-announcement-preset-alert", 8f),
        new(AnnouncementPreset.OnboardComputer, "FrontierOnboardComputer", "lua-announcement-preset-onboard-computer", 7f)
    };

    public static IReadOnlyList<AnnouncementPresetDefinition> All => Definitions;

    public static string GetId(AnnouncementPreset preset) => GetDefinition(preset).Id;
    public static LocId GetName(AnnouncementPreset preset) => GetDefinition(preset).Name;
    public static float GetPriority(AnnouncementPreset preset) => GetDefinition(preset).Priority;

    public static AnnouncementPresetDefinition GetDefinition(AnnouncementPreset preset)
    {
        return preset switch
        {
            AnnouncementPreset.Comms => Definitions[0],
            AnnouncementPreset.Faction => Definitions[1],
            AnnouncementPreset.Alert => Definitions[2],
            AnnouncementPreset.OnboardComputer => Definitions[3],
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null)
        };
    }
}
