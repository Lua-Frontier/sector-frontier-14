using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.ErpPanel.Requirements;

[Serializable, NetSerializable]
public sealed partial class MarkingCategoryRequirement : InvertableErpRequirement
{
    [DataField(required: true)]
    public MarkingCategories Category;

    public override bool IsAvailable(EntityUid uid, IEntityManager entityManager)
    {
        var hasMarking = entityManager.TryGetComponent<HumanoidAppearanceComponent>(uid, out var humanoid)
            && humanoid.MarkingSet.TryGetCategory(Category, out var markings)
            && markings.Count > 0;

        return Inverted ? !hasMarking : hasMarking;
    }
}
