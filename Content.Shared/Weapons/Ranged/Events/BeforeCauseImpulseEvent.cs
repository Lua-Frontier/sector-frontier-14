namespace Content.Shared.Weapons.Ranged.Events;

[ByRefEvent]
public record struct BeforeCauseImpulseEvent(bool Cancelled);
