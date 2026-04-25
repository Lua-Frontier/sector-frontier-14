namespace Content.Shared.Blocking;

[ByRefEvent]
public readonly record struct ShieldBlockedDamageEvent(float TotalBlockedDamage, float BallisticBlockedDamage);

[ByRefEvent]
public readonly record struct ShieldReflectedDamageEvent(float TotalReflectedDamage);
