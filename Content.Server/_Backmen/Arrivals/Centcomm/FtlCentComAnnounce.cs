using Robust.Shared.GameObjects;

namespace Content.Server.Backmen.Arrivals.CentComm;

public sealed class FtlCentComAnnounce : EntityEventArgs
{
    public EntityUid Source { get; set; }
}
