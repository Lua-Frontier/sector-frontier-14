using Content.Shared.Research.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Research.Discovery;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ResearchSourceComponent : Component
{
    [DataField, AutoNetworkedField]
    public int TechYieldTotal;

    [DataField, AutoNetworkedField]
    public int TechYieldRemaining = -1;

    [DataField, AutoNetworkedField]
    public List<ProtoId<TechnologyPrototype>> DiscoveredTechs = new();

    [DataField]
    public float TechDiscoveryChance = DiscoveryResearchConstants.TechDiscoveryChance;
}
