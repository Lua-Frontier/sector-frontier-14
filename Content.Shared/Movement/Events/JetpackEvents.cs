namespace Content.Shared.Movement.Events;

[ByRefEvent]
public readonly record struct JetpackEnabledEvent(EntityUid User, EntityUid Jetpack);
