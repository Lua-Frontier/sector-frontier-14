using Content.Shared.Physics;
using Robust.Shared.Prototypes;

namespace Content.Server._Goobstation.MobCaller;

[RegisterComponent]
public sealed partial class MobCallerComponent : Component
{
    [DataField]
    public TimeSpan SpawnSpacing = TimeSpan.FromSeconds(30);

    [DataField(required: true)]
    public EntProtoId SpawnProto;

    [DataField]
    public int MaxAlive = 5;

    [DataField]
    public int SpawnDirections = 16;

    [DataField]
    public float OcclusionDistance = 30f;

    [DataField]
    public float GridOcclusionDistance = 120f;

    [DataField]
    public float GridOcclusionFidelity = 5f;

    [DataField]
    public CollisionGroup OcclusionMask = CollisionGroup.Impassable;

    [DataField]
    public float MinDistance = 50f;

    [DataField]
    public float MaxDistance = 120f;

    [DataField]
    public bool NeedAnchored = true;

    [DataField]
    public bool NeedPower = true;

    [DataField]
    public TimeSpan SpawnAccumulator = TimeSpan.FromSeconds(0);

    [DataField]
    public List<EntityUid> SpawnedEntities = new();

    [ViewVariables]
    public TimeSpan LastExamineRaycast = TimeSpan.FromSeconds(0);

    [ViewVariables]
    public TimeSpan ExamineRaycastSpacing = TimeSpan.FromSeconds(1);

    [ViewVariables]
    public bool CachedExamineResult = false;
}

