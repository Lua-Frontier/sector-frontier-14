using Content.Server.Administration.Logs;
using Content.Shared.Database;
using Robust.Shared.Map;
using Robust.Shared.Placement;
using Robust.Shared.Player;

namespace Content.Server.Placement;

public sealed class PlacementLoggerSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefinitionManager = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlacementEntityEvent>(OnEntityPlacement);
        SubscribeLocalEvent<PlacementTileEvent>(OnTilePlacement);
    }

    private void OnEntityPlacement(PlacementEntityEvent ev)
    {
        _player.TryGetSessionById(ev.PlacerNetUserId, out var actor);
        var actorEntity = actor?.AttachedEntity;

        var logType = ev.PlacementEventAction switch
        {
            PlacementEventAction.Create => LogType.EntitySpawn,
            PlacementEventAction.Erase => LogType.EntityDelete,
            _ => LogType.Action
        };

        var impact = ev.PlacementEventAction == PlacementEventAction.Create
            ? LogImpact.Extreme
            : LogImpact.Medium;

        var action = ev.PlacementEventAction switch
        {
            PlacementEventAction.Create => "spawned",
            PlacementEventAction.Erase => "deleted",
            _ => ev.PlacementEventAction.ToString().ToLowerInvariant()
        };

        if (actorEntity != null)
            _adminLogger.Add(logType, impact,
                $"{ToPrettyString(actorEntity.Value):user} {action} {ToPrettyString(ev.EditedEntity):entity} at {ev.Coordinates:coordinates}");
        else if (actor != null)
            _adminLogger.Add(logType, impact,
                $"{actor:user} {action} {ToPrettyString(ev.EditedEntity):entity} at {ev.Coordinates:coordinates}");
        else
            _adminLogger.Add(logType, impact,
                $"{action} {ToPrettyString(ev.EditedEntity):entity} at {ev.Coordinates:coordinates}");
    }

    private void OnTilePlacement(PlacementTileEvent ev)
    {
        _player.TryGetSessionById(ev.PlacerNetUserId, out var actor);
        var actorEntity = actor?.AttachedEntity;

        if (actorEntity != null)
            _adminLogger.Add(LogType.Tile, LogImpact.Medium,
                $"{ToPrettyString(actorEntity.Value):user} set tile {_tileDefinitionManager[ev.TileType].Name} at {ev.Coordinates:coordinates}");
        else if (actor != null)
            _adminLogger.Add(LogType.Tile, LogImpact.Medium,
                $"{actor:user} set tile {_tileDefinitionManager[ev.TileType].Name} at {ev.Coordinates:coordinates}");
        else
            _adminLogger.Add(LogType.Tile, LogImpact.Medium,
                $"set tile {_tileDefinitionManager[ev.TileType].Name} at {ev.Coordinates:coordinates}");
    }
}
