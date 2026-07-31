using Content.Server._Lua.ShipTracker.Components;
using Content.Server._Lua.ShipTracker.Events;
using Content.Server.Chat.Systems;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Shared._Lua.ShipTracker;
using Robust.Shared.Audio;
using Robust.Shared.Map;

namespace Content.Server._Lua.ShipTracker.Systems;

/// <summary>
/// This handles tracking ships, healths and more
/// </summary>
public sealed partial class ShipTrackerSystem : SharedShipTrackerSystem
{
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private readonly HashSet<EntityUid> _gridsWithConsoles = new();

    public override void Initialize()
    {
        base.Initialize();

        //SubscribeLocalEvent<ShipTrackerComponent, FTLStartedEvent>(OnFTLStartedEvent);
    }


    private void BroadcastToStationsOnMap(
        MapId map,
        string message,
        string sender = "Automated Ship",
        bool playDefaultSound = true,
        SoundSpecifier? announcementSound = null,
        Color? colorOverride = null)
    {
        var query = EntityQueryEnumerator<ShipTrackerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapID != map)
                continue;

            _chatSystem.DispatchStationAnnouncement(uid, message, sender, playDefaultSound, announcementSound, colorOverride);
        }
    }

    private void OnFTLStartedEvent(EntityUid uid, ShipTrackerComponent component, ref FTLStartedEvent args)
    {
        // alert those who are going onto map
        // BroadcastToStationsOnMap(args.TargetCoordinates.GetMapId(_entityManager), Loc.GetString("ship-ftl-jump-jumped-message"), colorOverride: Color.Gold);
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
