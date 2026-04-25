using Content.Shared.Construction;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.Examine;
using JetBrains.Annotations;

namespace Content.Server.Construction.Conditions;

[UsedImplicitly]
[DataDefinition]
public sealed partial class NoUnstackableInTileGraph : IGraphCondition
{
    public bool Condition(EntityUid uid, IEntityManager entityManager)
    {
        var transform = entityManager.GetComponent<TransformComponent>(uid);
        var anchorable = entityManager.System<AnchorableSystem>();
        return !anchorable.AnyUnstackablesAnchoredAt(transform.Coordinates);
    }

    public bool DoExamine(ExaminedEvent args) => false;

    public IEnumerable<ConstructionGuideEntry> GenerateGuideEntry()
    {
        yield return new ConstructionGuideEntry
        { Localization = "construction-step-condition-no-unstackable-in-tile", };
    }
}
