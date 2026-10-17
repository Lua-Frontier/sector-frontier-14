using JetBrains.Annotations;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.ErpPanel.Requirements;

[ImplicitDataDefinitionForInheritors, Serializable, NetSerializable]
[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public abstract partial class ErpRequirement
{
    public abstract bool IsAvailable(EntityUid uid, IEntityManager entityManager);
}

[Serializable, NetSerializable]
public abstract partial class InvertableErpRequirement : ErpRequirement
{
    [DataField] public bool Inverted = false;
}
