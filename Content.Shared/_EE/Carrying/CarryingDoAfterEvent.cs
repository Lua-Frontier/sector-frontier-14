using Content.Shared.DoAfter;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.Carrying;

[Serializable, NetSerializable]
public sealed partial class CarryDoAfterEvent : SimpleDoAfterEvent { }

[ByRefEvent]
public readonly record struct CarryStartedEvent(EntityUid Carrier, EntityUid Carried);
