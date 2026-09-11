namespace Content.Shared.Blocking;

[ByRefEvent]
public readonly record struct ShieldReflectedDamageEvent(float TotalReflectedDamage);
