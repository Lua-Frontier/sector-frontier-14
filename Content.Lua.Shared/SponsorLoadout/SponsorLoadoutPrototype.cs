// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.Prototypes;

namespace Content.Lua.Shared.SponsorLoadout;

[Prototype("sponsorLoadout")]
public sealed partial class SponsorLoadoutPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("ownerLogin", required: true)]
    public string OwnerLogin { get; private set; } = default!;

    [DataField("tier")]
    public string? Tier { get; private set; }

    [DataField("entities", required: true)]
    public List<EntProtoId> Entities { get; private set; } = new();
}
