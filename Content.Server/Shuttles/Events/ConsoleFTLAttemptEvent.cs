namespace Content.Server.Shuttles.Events;

using Robust.Shared.Map;

/// <summary>
/// Raised when a shuttle console is trying to FTL via UI input.
/// </summary>
/// <param name="Cancelled"></param>
/// <param name="Reason"></param>
[ByRefEvent]
public record struct ConsoleFTLAttemptEvent(EntityUid Uid, bool Cancelled, string Reason, EntityCoordinates? Destination = null);
