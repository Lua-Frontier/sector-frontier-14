using Content.Shared.Standing;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.ErpPanel.Requirements;

[Serializable, NetSerializable]
public sealed partial class TargetOnFloorRequirement : InvertableErpRequirement
{
    public override bool IsAvailable(EntityUid uid, IEntityManager entityManager)
    {
        var isOnFloor = entityManager.System<StandingStateSystem>().IsDown(uid);

        return Inverted ? !isOnFloor : isOnFloor;
    }
}
