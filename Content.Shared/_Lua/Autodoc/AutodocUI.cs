using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Autodoc;

[Serializable, NetSerializable]
public enum AutodocUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public enum AutodocOperationKind : byte
{
    RemovePart,
    AttachPart,
    RemoveOrgan,
    AttachOrgan,
    TendWounds
}

[Serializable, NetSerializable]
public enum AutodocTransferTarget : byte
{
    BodyPart,
    OrganSlot,
    Storage
}

[Serializable, NetSerializable]
public sealed class AutodocBodyPartInfo(NetEntity entity, string name, string slot, float integrity)
{
    public readonly NetEntity Entity = entity;
    public readonly string Name = name;
    public readonly string Slot = slot;
    public readonly float Integrity = integrity;
}

[Serializable, NetSerializable]
public sealed class AutodocOrganInfo(string slot, NetEntity? entity, string? name)
{
    public readonly string Slot = slot;
    public readonly NetEntity? Entity = entity;
    public readonly string? Name = name;
}

[Serializable, NetSerializable]
public sealed class AutodocStorageItemInfo(NetEntity entity, string name, bool isBodyPart, string? organSlot)
{
    public readonly NetEntity Entity = entity;
    public readonly string Name = name;
    public readonly bool IsBodyPart = isBodyPart;
    public readonly string? OrganSlot = organSlot;
    public readonly string? BodyPartSlot;

    public AutodocStorageItemInfo(NetEntity entity, string name, string bodyPartSlot) : this(entity, name, true, null)
    {
        BodyPartSlot = bodyPartSlot;
    }
}

[Serializable, NetSerializable]
public sealed class AutodocDamagedPartInfo(string name, string slot, Dictionary<string, FixedPoint2> damage)
{
    public readonly string Name = name;
    public readonly string Slot = slot;
    public readonly Dictionary<string, FixedPoint2> Damage = damage;
}

[Serializable, NetSerializable]
public sealed class AutodocPatientVitals(
    string name,
    string? speciesId,
    MobState? mobState,
    float temperature,
    float bloodLevel,
    bool bleeding,
    float totalDamage,
    Dictionary<string, FixedPoint2> damagePerGroup,
    Dictionary<string, FixedPoint2> damagePerType,
    List<AutodocDamagedPartInfo> damagedParts,
    Dictionary<string, FixedPoint2>? selectedPartDamage)
{
    public readonly string Name = name;
    public readonly string? SpeciesId = speciesId;
    public readonly MobState? MobState = mobState;
    public readonly float Temperature = temperature;
    public readonly float BloodLevel = bloodLevel;
    public readonly bool Bleeding = bleeding;
    public readonly float TotalDamage = totalDamage;
    public readonly Dictionary<string, FixedPoint2> DamagePerGroup = damagePerGroup;
    public readonly Dictionary<string, FixedPoint2> DamagePerType = damagePerType;
    public readonly List<AutodocDamagedPartInfo> DamagedParts = damagedParts;
    public readonly Dictionary<string, FixedPoint2>? SelectedPartDamage = selectedPartDamage;
}

[Serializable, NetSerializable]
public sealed class AutodocBoundUserInterfaceState(
    NetEntity? patient,
    List<AutodocBodyPartInfo> parts,
    NetEntity? selectedPart,
    List<AutodocOrganInfo> organs,
    List<AutodocStorageItemInfo> storage,
    AutodocPatientVitals? vitals,
    bool busy,
    float progress,
    float progressTarget,
    TimeSpan? progressStart,
    TimeSpan? progressEnd,
    string status) : BoundUserInterfaceState
{
    public readonly NetEntity? Patient = patient;
    public bool PatientPresent => Patient != null;
    public readonly List<AutodocBodyPartInfo> Parts = parts;
    public readonly NetEntity? SelectedPart = selectedPart;
    public readonly List<AutodocOrganInfo> Organs = organs;
    public readonly List<AutodocStorageItemInfo> Storage = storage;
    public readonly AutodocPatientVitals? Vitals = vitals;
    public readonly bool Busy = busy;
    public readonly float Progress = progress;
    public readonly float ProgressTarget = progressTarget;
    public readonly TimeSpan? ProgressStart = progressStart;
    public readonly TimeSpan? ProgressEnd = progressEnd;
    public readonly string Status = status;
}

[Serializable, NetSerializable]
public sealed class AutodocSelectPartMessage(NetEntity part) : BoundUserInterfaceMessage
{
    public readonly NetEntity Part = part;
}

[Serializable, NetSerializable]
public sealed class AutodocRemovePartMessage(NetEntity part) : BoundUserInterfaceMessage
{
    public readonly NetEntity Part = part;
}

[Serializable, NetSerializable]
public sealed class AutodocHealPartMessage(NetEntity part) : BoundUserInterfaceMessage
{
    public readonly NetEntity Part = part;
}

[Serializable, NetSerializable]
public sealed class AutodocTransferMessage(
    NetEntity item,
    AutodocTransferTarget source,
    AutodocTransferTarget destination,
    NetEntity? targetPart = null,
    string? organSlot = null) : BoundUserInterfaceMessage
{
    public readonly NetEntity Item = item;
    public readonly AutodocTransferTarget Source = source;
    public readonly AutodocTransferTarget Destination = destination;
    public readonly NetEntity? TargetPart = targetPart;
    public readonly string? OrganSlot = organSlot;
}

[Serializable, NetSerializable]
public sealed class AutodocStopMessage : BoundUserInterfaceMessage;
