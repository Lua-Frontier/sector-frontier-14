namespace Content.Shared.Shuttles.Events;

public readonly record struct ThrusterDisabledByUserEvent(EntityUid User, EntityUid Thruster);
