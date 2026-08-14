namespace Content.Shared._RMC14.Wieldable.Events;

/// <summary>
/// Raised to allow other systems to modify the effective wield/draw delay.
/// </summary>
[ByRefEvent]
public record struct GetWieldDelayEvent(TimeSpan Delay);
