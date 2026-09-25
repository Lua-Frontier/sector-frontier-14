using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Research.Discovery;

[Prototype("factionResearchProfile")]
public sealed partial class FactionResearchProfilePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<ProtoId<TechnologyPrototype>> StartingKits = new();
    [DataField]
    public Dictionary<string, float> AffinityTags = new();

    [DataField]
    public List<ProtoId<TechnologyPrototype>> SignatureTechs = new();

    [DataField]
    public Dictionary<ProtoId<TechDisciplinePrototype>, float> DisciplineBias = new();
    [DataField]
    public float SignatureWeightBonus = 2.5f;
}
