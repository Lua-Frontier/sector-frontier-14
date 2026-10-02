using System;
using System.Collections.Generic;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.Shared._Arcane.ErpPanel.Requirements;

[Serializable, NetSerializable]
public sealed partial class NearbyStaticEntityRequirement : InvertableErpRequirement
{
    [DataField]
    public float Range = 0.5f;

    [DataField(required: true)]
    public HashSet<ProtoId<TagPrototype>> Tags = new();

    public override bool IsAvailable(EntityUid uid, IEntityManager entityManager)
    {
        var lookupSystem = entityManager.System<EntityLookupSystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var tagSystem = entityManager.System<TagSystem>();

        var nearbyEntities = lookupSystem.GetEntitiesInRange(uid, Range);

        var hasNearbyStatic = false;

        foreach (var entity in nearbyEntities)
        {
            if (entity == uid)
                continue;

            if (containerSystem.IsEntityInContainer(entity))
                continue;

            if (entityManager.TryGetComponent<PhysicsComponent>(entity, out var physics) &&
                physics.BodyType == BodyType.Static)
            {
                if (!tagSystem.HasAnyTag(entity, Tags))
                    continue;

                hasNearbyStatic = true;
                break;
            }
        }

        return Inverted ? !hasNearbyStatic : hasNearbyStatic;
    }
}
