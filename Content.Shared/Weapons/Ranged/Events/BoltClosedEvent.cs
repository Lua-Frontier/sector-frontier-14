namespace Content.Shared.Weapons.Ranged.Events;

[ByRefEvent]
public readonly record struct BoltClosedEvent(EntityUid? User);
