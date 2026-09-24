// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
//This content is sourced from Мёртвый Космос and is used with explicit permission for use in Sector Frontier(LuaWorld) https://github.com/HacksLua/sector-frontier-14.
// Мёртвый Космос - This file is licensed under AGPLv3
// Copyright (c) 2025 Мёртвый Космос Contributors
// See AGPLv3.txt for details.

using Content.Shared.Containers.ItemSlots;
using Content.Lua.Shared._DeadSpace.Photocopier;
using Content.Shared.Paper;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Lua.Server._DeadSpace.Photocopier;

[RegisterComponent]
public sealed partial class PhotocopierComponent : Component
{
    [DataField(required: true)]
    public ItemSlot PaperSlot = new();

    [DataField]
    public SoundSpecifier EmagSound = new SoundCollectionSpecifier("sparks");

    [DataField]
    public bool WasEmagged = false;

    [DataField]
    public SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");

    [DataField]
    public SoundSpecifier ScanSound = new SoundPathSpecifier("/Audio/_DeadSpace/Machines/scanning.ogg");

    [ViewVariables]
    [DataField]
    public Queue<PhotocopierPrintout> PrintingQueue { get; private set; } = new();

    [ViewVariables]
    [DataField]
    public float UseTimeoutRemaining;

    [ViewVariables]
    [DataField]
    public float UseTimeout = 2.6f;

    [DataField]
    public float ScanningTimeRemaining;

    [ViewVariables]
    public float ScanningTime = 12.1f;

    [DataField]
    public float PrintingTimeRemaining;

    [ViewVariables]
    public float PrintingTime = 2.3f;

    [ViewVariables]
    public PhotocopierMode Mode = PhotocopierMode.Copy;

    [DataField]
    public PhotocopierType PhotocopierType = PhotocopierType.Default;

    [DataField]
    public PaperworkFormPrototype? ChosenPaper = null;

    [ViewVariables]
    public int TonerLeft = 30;

    [ViewVariables]
    public int MaxTonerAmount = 30;

    [DataField]
    public SoundSpecifier TonerRestock = new SoundPathSpecifier("/Audio/_DeadSpace/Machines/vending_restock_done_cuted.ogg");
}

[DataDefinition]
public sealed partial class PhotocopierPrintout
{
    [DataField("name", required: true)]
    public string Name { get; private set; } = default!;

    [DataField("content", required: true)]
    public string Content { get; private set; } = default!;

    [DataField("prototypeId", required: true)]
    public EntProtoId PrototypeId { get; private set; } = default!;

    [DataField("stampState")]
    public string? StampState { get; private set; }

    [DataField("stampedBy")]
    public List<StampDisplayInfo> StampedBy { get; private set; } = new();

    public PhotocopierPrintout(string content, string name, string prototypeId, string? stampState = null, List<StampDisplayInfo>? stampedBy = null)
    {
        Content = content;
        Name = name;
        PrototypeId = prototypeId;
        StampState = stampState;
        StampedBy = stampedBy ?? new List<StampDisplayInfo>();
    }
}
