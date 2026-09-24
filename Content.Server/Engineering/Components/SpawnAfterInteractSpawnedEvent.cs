namespace Content.Server.Engineering.Components;

[ByRefEvent]
public readonly record struct SpawnAfterInteractSpawnedEvent(EntityUid User, EntityUid Source);
