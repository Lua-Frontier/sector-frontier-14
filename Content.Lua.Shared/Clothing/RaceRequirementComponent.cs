using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Prototypes; // Для ProtoId<>
using Content.Shared.Humanoid.Prototypes; // Для SpeciesPrototype

namespace Content.Shared.Clothing.Components;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class RaceRequirementComponent : Component
{
    [DataField("enabled")]
    [AutoNetworkedField]
    public bool Enabled = true;

    [DataField("allowedRaces")]
    [AutoNetworkedField]
    public HashSet<ProtoId<SpeciesPrototype>> AllowedRaces = new();
}
