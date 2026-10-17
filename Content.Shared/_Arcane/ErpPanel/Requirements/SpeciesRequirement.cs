using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.ErpPanel.Requirements;

[Serializable, NetSerializable]
public sealed partial class SpeciesRequirement : InvertableErpRequirement
{
    [DataField(required: true)]
    public HashSet<ProtoId<SpeciesPrototype>> Species = new();

    public override bool IsAvailable(EntityUid uid, IEntityManager entityManager)
    {
        if (!entityManager.TryGetComponent<HumanoidAppearanceComponent>(uid, out var humanoid))
            return false;

        var hasSpecies = Species.Contains(humanoid.Species);

        return Inverted ? !hasSpecies : hasSpecies;
    }
}
