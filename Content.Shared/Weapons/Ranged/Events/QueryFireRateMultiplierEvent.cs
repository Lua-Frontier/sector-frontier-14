namespace Content.Shared.Weapons.Ranged.Events;

/// <summary>
/// Raised on a gun to multiply the delay until the next shot. Mono.
/// </summary>
[ByRefEvent]
public record struct QueryFireRateMultiplierEvent(float ReloadTimeMul = 1f);
