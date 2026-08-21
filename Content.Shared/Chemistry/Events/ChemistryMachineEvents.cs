using Robust.Shared.GameObjects;

namespace Content.Shared.Chemistry.Events;

[ByRefEvent]
public readonly record struct ChemistryMachineUiOpenedEvent(EntityUid Machine, EntityUid? User);
