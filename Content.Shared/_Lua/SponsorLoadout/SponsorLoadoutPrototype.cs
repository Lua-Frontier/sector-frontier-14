// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;

namespace Content.Shared._Lua.SponsorLoadout;

[Prototype]
public sealed partial class SponsorLoadoutPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("ownerLogin", required: true)]
    public string OwnerLogin { get; private set; } = default!;

    [DataField("tier")]
    public string? Tier { get; private set; }

    [DataField("entities", required: true, customTypeSerializer: typeof(PrototypeIdListSerializer<EntityPrototype>))]
    public List<string> Entities { get; private set; } = new();
}
