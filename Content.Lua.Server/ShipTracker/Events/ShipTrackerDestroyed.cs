using Content.Lua.Server.ShipTracker.Components;

namespace Content.Lua.Server.ShipTracker.Events;

public sealed class ShipTrackerDestroyed : EntityEventArgs
{
    public EntityUid Ship;
    public ShipTrackerComponent Component;

    public ShipTrackerDestroyed(EntityUid ship, ShipTrackerComponent component)
    {
        Ship = ship;
        Component = component;
    }
}
