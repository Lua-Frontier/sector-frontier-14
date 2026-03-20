namespace Content.Shared._Lua.Stargate;

public sealed class AttemptPlanetaryRCDUseEvent : EntityEventArgs
{
    public EntityUid GridUid;
    public bool Allowed = true;

    public AttemptPlanetaryRCDUseEvent(EntityUid gridUid)
    {
        GridUid = gridUid;
    }
}
