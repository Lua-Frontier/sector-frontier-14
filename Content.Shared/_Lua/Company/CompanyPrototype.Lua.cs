// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

namespace Content.Shared._Mono.Company;

public sealed partial class CompanyPrototype
{
    [DataField("hiddenFromNonMembers")]
    public bool HiddenFromNonMembers { get; private set; } = false;

    [DataField("leaderJobs")]
    public List<string> LeaderJobs { get; private set; } = new();
}

