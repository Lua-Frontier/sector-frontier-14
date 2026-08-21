// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Prototypes;

namespace Content.Shared._Lua.LunaPlan;

[Prototype]
public sealed partial class LunaPlanPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Headline { get; private set; }

    [DataField(required: true)]
    public LocId Summary { get; private set; }

    [DataField(required: true)]
    public LunaPlanStatus Status { get; private set; } = LunaPlanStatus.Queued;

    [DataField]
    public int Sort { get; private set; }

    [DataField]
    public List<LocId> Labels { get; private set; } = new();
}

public enum LunaPlanStatus : byte
{
    Done = 0,
    Active = 1,
    Queued = 2,
}
