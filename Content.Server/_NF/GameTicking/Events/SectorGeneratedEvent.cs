namespace Content.Server._NF.GameTicking.Events;

using Robust.Shared.Map;

public sealed class StationsGeneratedEvent : EntityEventArgs;

public sealed class SectorLoadedEvent : EntityEventArgs
{
    public string SectorId { get; }

    public SectorLoadedEvent(string sectorId)
    {
        SectorId = sectorId;
    }
}

public sealed class SectorUnloadedEvent : EntityEventArgs
{
    public string SectorId { get; }
    public MapId MapId { get; }

    public SectorUnloadedEvent(string sectorId, MapId mapId)
    {
        SectorId = sectorId;
        MapId = mapId;
    }
}
