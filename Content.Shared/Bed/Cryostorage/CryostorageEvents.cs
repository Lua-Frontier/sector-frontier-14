using Robust.Shared.GameObjects;

namespace Content.Shared.Bed.Cryostorage;

[ByRefEvent]
public readonly record struct CryostorageEnteredEvent(EntityUid User, EntityUid Cryostorage);
