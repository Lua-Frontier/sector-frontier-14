// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Shared.Research.Components;

namespace Content.Server.Research.Systems;

public sealed partial class ResearchSystem
{
    public bool TryStartResearchProject(EntityUid client, string technologyId, EntityUid user, ResearchClientComponent? clientComp = null)
    {
        return false;
    }

    public bool TryCancelResearchProject(EntityUid client, string technologyId, EntityUid user, ResearchClientComponent? clientComp = null)
    {
        return false;
    }
}
