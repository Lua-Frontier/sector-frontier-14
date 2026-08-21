namespace Content.Shared.Bible;

[ByRefEvent]
public readonly record struct BibleHealAttemptEvent(EntityUid User, EntityUid Target);
