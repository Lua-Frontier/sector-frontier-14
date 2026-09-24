// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.Audio;

namespace Content.Shared._Mono.Company;

public sealed partial class CompanyPrototype
{
    [DataField("hiddenFromNonMembers")]
    public bool HiddenFromNonMembers { get; private set; } = false;

    [DataField("motd")]
    public string? Motd { get; private set; }

    [DataField("leaderJobs")]
    public List<string> LeaderJobs { get; private set; } = new();

    [DataField]
    public string? AnnouncementTitle { get; private set; }

    [DataField]
    public Color? AnnouncementColor { get; private set; }

    [DataField]
    public SoundSpecifier? AnnouncementSound { get; private set; }
}

