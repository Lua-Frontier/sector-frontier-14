using Robust.Shared.GameObjects;

namespace Content.Shared.Kitchen.Events;

[ByRefEvent]
public readonly record struct MicrowaveCookStartedEvent(EntityUid Microwave, EntityUid? User);
