using Content.Lua.Server.ShipTracker.Components;
using Content.Lua.Server.ShipTracker.Events;
using Content.Server.Shuttles.Components;
using Content.Lua.Shared.ShipTracker;

namespace Content.Lua.Server.ShipTracker.Systems;

public sealed partial class ShipTrackerSystem : SharedShipTrackerSystem
{
    private readonly HashSet<EntityUid> _gridsWithConsoles = new();

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _gridsWithConsoles.Clear();
        var consoles = EntityQueryEnumerator<ShuttleConsoleComponent, TransformComponent>();
        while (consoles.MoveNext(out _, out _, out var consoleXform))
        {
            if (consoleXform.GridUid is { } gridUid)
                _gridsWithConsoles.Add(gridUid);
        }

        var allShips = EntityQueryEnumerator<ShipTrackerComponent>();
        while (allShips.MoveNext(out var entity, out var shipTrackerComponent))
        {
            if (shipTrackerComponent.Destroyed)
                continue;

            if (_gridsWithConsoles.Contains(entity))
            {
                shipTrackerComponent.SecondsWithoutPiloting = 0f;
                continue;
            }

            shipTrackerComponent.SecondsWithoutPiloting += frameTime;
            if (shipTrackerComponent.SecondsWithoutPiloting < shipTrackerComponent.CallDestroyedSeconds)
                continue;

            var ev = new ShipTrackerDestroyed(entity, shipTrackerComponent);
            RaiseLocalEvent(ev);

            shipTrackerComponent.Destroyed = true;
        }
    }
}
