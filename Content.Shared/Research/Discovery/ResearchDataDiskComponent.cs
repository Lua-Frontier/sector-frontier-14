using Content.Shared.Research.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Research.Discovery;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ResearchDataDiskComponent : Component
{
    [DataField, AutoNetworkedField]
    public ResearchDataDiskStage Stage = ResearchDataDiskStage.Empty;

    [DataField, AutoNetworkedField]
    public ProtoId<TechnologyPrototype>? LockedTechId;

    [DataField, AutoNetworkedField]
    public ProtoId<TechDisciplinePrototype>? LeadDiscipline;

    [DataField, AutoNetworkedField]
    public int LeadTierHint;

    [DataField, AutoNetworkedField]
    public List<string> LeadTags = new();

    [DataField, AutoNetworkedField]
    public string LeadRisk = string.Empty;

    [DataField, AutoNetworkedField]
    public int LeadCostEstimate;

    [DataField, AutoNetworkedField]
    public float DecodeProgress;

    [DataField, AutoNetworkedField]
    public float DecodeCost;
}

[Serializable, NetSerializable]
public enum ResearchDataDiskVisuals : byte
{
    Stage,
}
