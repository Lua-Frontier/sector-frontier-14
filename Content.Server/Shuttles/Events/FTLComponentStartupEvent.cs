namespace Content.Server.Shuttles.Events;

/// <summary>
/// Broadcast when <see cref="Content.Shared.Shuttles.Components.FTLComponent"/> starts.
/// Directed (FTLComponent, ComponentStartup) is owned by BluespaceFuelSystem.
/// </summary>
[ByRefEvent]
public readonly record struct FTLComponentStartupEvent(EntityUid Entity);
