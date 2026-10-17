using Content.Shared._Arcane.ErpPanel.Requirements;
using Content.Shared.Dataset;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Arcane.ErpPanel;

public enum PanelInteractionKind : byte
{
    Safe,
    Erotic,
    Harmful,
    HarmfulErotic, // Lua
}

[Prototype]
public sealed partial class PanelInteractionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public ProtoId<PanelInteractionCategoryPrototype> Category;

    [DataField(required: true)]
    public PanelInteractionKind Kind;

    [DataField]
    public ProtoId<LocalizedDatasetPrototype>? Messages;

    [DataField]
    public ProtoId<LocalizedDatasetPrototype>? SelfMessages;

    [DataField(required: true)]
    public SpriteSpecifier Icon = SpriteSpecifier.Invalid;

    [DataField]
    public SoundSpecifier? Sound;

    [DataField]
    public float Range = 1f;

    [DataField]
    public int UserArouse = 0;

    [DataField]
    public int TargetArouse = 0;

    [DataField]
    public List<ErpRequirement>? UserRequirements;

    [DataField]
    public List<ErpRequirement>? TargetRequirements;

    public bool IsErotic => Kind is PanelInteractionKind.Erotic or PanelInteractionKind.HarmfulErotic; // Lua
}

[Prototype]
public sealed partial class PanelInteractionCategoryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;
}
