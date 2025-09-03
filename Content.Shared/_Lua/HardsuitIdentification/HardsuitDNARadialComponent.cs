// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.
// Special Permission:
// In addition to AGPLv3, the author grants the "Мёртвый Космос" project
// the right to use this code under a separate custom license agreement.

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Lua.HardsuitIdentification;

[RegisterComponent]
public sealed partial class HardsuitDNARadialComponent : Component
{
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string OpenDNARadialAction = "ActionHardsuitOpenDNARadial";

    [DataField]
    public EntityUid? OpenDNARadialActionEntity;
}
