// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
//This content is sourced from Мёртвый Космос and is used with explicit permission for use in Sector Frontier(LuaWorld) https://github.com/HacksLua/sector-frontier-14.
// Мёртвый Космос - This file is licensed under AGPLv3
// Copyright (c) 2025 Мёртвый Космос Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Lua.Shared._DeadSpace.Photocopier;

[Prototype("paperworkForm")][Serializable, NetSerializable]
public sealed partial class PaperworkFormPrototype : IPrototype
{
    [ViewVariables][IdDataField] public string ID { get; private set; } = default!;

    [DataField("category")] public string Category { get; private set; } = default!;

    [DataField("name", required: true)] public string Name = default!;

    [DataField("text", required: true)] public ResPath Text = default!;

    [DataField("paperPrototype")]
    public EntProtoId PaperPrototype = default!;
}
