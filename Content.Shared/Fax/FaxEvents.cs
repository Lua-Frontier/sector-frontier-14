namespace Content.Shared.Fax;

[ByRefEvent]
public readonly record struct FaxSentEvent(EntityUid User, EntityUid FaxMachine);
