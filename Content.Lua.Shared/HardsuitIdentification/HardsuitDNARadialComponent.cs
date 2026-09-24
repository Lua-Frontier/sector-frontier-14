// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.
// In addition to AGPLv3, the author grants the "Мёртвый Космос" project

using Robust.Shared.Prototypes;

namespace Content.Lua.Shared.HardsuitIdentification;

[RegisterComponent]
public sealed partial class HardsuitDNARadialComponent : Component
{
    public EntProtoId OpenDNARadialAction = "ActionHardsuitOpenDNARadial";

    [DataField]
    public EntityUid? OpenDNARadialActionEntity;
}
