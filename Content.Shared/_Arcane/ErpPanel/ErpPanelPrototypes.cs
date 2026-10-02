using Content.Shared._Arcane.ErpPanel.Requirements;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Arcane.ErpPanel;

public static class ErpPanelConstants
{
    public const string ErpInteractionTag = "RequiresERP";
}

[Prototype]
public sealed partial class PanelInteractionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public ProtoId<PanelInteractionCategoryPrototype> Category;

    [DataField]
    public List<string> Messages = new();

    [DataField]
    public List<string> SelfMessages = new();

    [DataField(required: true)]
    public SpriteSpecifier Icon = SpriteSpecifier.Invalid;

    [DataField]
    public List<ResPath> Sounds = new();

    [DataField]
    public HashSet<string> Tags = new();

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(3);

    [DataField]
    public float Range = 2f;

    [DataField]
    public int UserArouse = 0;

    [DataField]
    public int TargetArouse = 0;

    [DataField]
    public List<ErpRequirement>? UserRequirements;

    [DataField]
    public List<ErpRequirement>? TargetRequirements;
}

[Prototype]
public sealed partial class PanelInteractionCategoryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;
}
