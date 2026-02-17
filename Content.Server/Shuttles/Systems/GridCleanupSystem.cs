using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using Robust.Shared.Configuration;
using Content.Server.Salvage.Expeditions;
using Content.Server.Gateway.Components;
using Content.Server._Lua.MapperGrid; // Lua
using Content.Shared.Tiles;
using Content.Shared.Lua.CLVar; // Lua

namespace Content.Server.Shuttles.Systems;

/// <summary>
/// This system cleans up small grid fragments that have less than a specified number of tiles after a delay.
/// </summary>
public sealed class GridCleanupSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    // The minimum number of tiles a grid needs to avoid being cleaned up
    private const int MinimumTiles = 10;

    // The delay before cleaning up a small grid (in seconds)
    private const float CleanupDelay = 300.0f;

    // Dictionary to track grids scheduled for deletion
    private readonly Dictionary<EntityUid, TimeSpan> _pendingCleanup = new();
    private readonly List<EntityUid> _pendingCleanupRemoveBuffer = new();
    private bool _cleanupEnabled;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, CLVars.AutoGridCleanupEnabled, v =>
        {
            _cleanupEnabled = v;
            if (!v)
                _pendingCleanup.Clear();
        }, true);

        // Subscribe to grid events
        SubscribeLocalEvent<GridStartupEvent>(OnGridStartup);
        SubscribeLocalEvent<MapGridComponent, TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<SalvageExpeditionComponent, ComponentStartup>(OnExpeditionStartup);
    }

    private bool IsCleanupEnabled()
    {
        return _cleanupEnabled;
    }

    private void OnGridStartup(GridStartupEvent ev)
    {
        // Check newly created grids
        if (TryComp<MapGridComponent>(ev.EntityUid, out var grid))
            CheckGrid((ev.EntityUid, grid));
    }

    private void OnTileChanged(Entity<MapGridComponent> ent, ref TileChangedEvent args)
    {
        // When a grid is modified (tiles added/removed), check if it needs cleanup
        CheckGrid(ent);
    }

    private void OnExpeditionStartup(EntityUid uid, SalvageExpeditionComponent component, ComponentStartup args)
    {
        // Make sure any grid that gets the expedition component is removed from cleanup
        if (_pendingCleanup.ContainsKey(uid))
        {
            Logger.DebugS("salvage", $"Expedition startup: Removing grid {uid} from cleanup queue");
            _pendingCleanup.Remove(uid);
        }

        // Check if this entity also has a grid component and ensure it's not marked for cleanup
        if (TryComp<MapGridComponent>(uid, out var grid))
        {
            // Make sure we don't clean up very small expedition grids
            var tileCount = CountTiles((uid, grid));
            Logger.DebugS("salvage", $"Expedition grid {uid} has {tileCount} tiles");
        }
    }

    private void CheckGrid(Entity<MapGridComponent> ent)
    {
        if (!IsCleanupEnabled())
            return;

        var gridUid = ent.Owner;
        var grid = ent.Comp;

        if (HasComp<GatewayGeneratorDestinationComponent>(gridUid) || HasComp<MapperGridComponent>(gridUid))
            return;

        // Skip if already scheduled for deletion
        if (_pendingCleanup.ContainsKey(gridUid))
            return;

        // Skip if this is a planet expedition grid
        if (HasComp<SalvageExpeditionComponent>(gridUid))
        {
            //Logger.DebugS("salvage", $"CheckGrid: Skipping grid {gridUid} with SalvageExpeditionComponent");
            return;
        }

        // Skip if the parent map has a SalvageExpeditionComponent
        var transform = Transform(gridUid);
        var mapId = transform.MapID;
        var mapUid = _mapManager.GetMapEntityId(mapId);

        if (HasComp<SalvageExpeditionComponent>(mapUid))
        {
            //Logger.DebugS("salvage", $"CheckGrid: Skipping grid {gridUid} on expedition map {mapUid}");
            return;
        }

        // Count tiles
        var tileCount = CountTiles((gridUid, grid));

        // If the tile count is below our threshold, schedule it for deletion
        if (tileCount < MinimumTiles)
        {
            Logger.DebugS("salvage", $"CheckGrid: Scheduling grid {gridUid} for cleanup with {tileCount} tiles");
            ScheduleGridCleanup(gridUid);
        }
    }

    private void ScheduleGridCleanup(EntityUid gridUid)
    {
        // Skip if already scheduled
        if (_pendingCleanup.ContainsKey(gridUid))
            return;

        var targetTime = _timing.CurTime + TimeSpan.FromSeconds(CleanupDelay);
        _pendingCleanup[gridUid] = targetTime;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!IsCleanupEnabled() || _pendingCleanup.Count == 0)
            return;

        // Check if any grids need to be cleaned up
        var currentTime = _timing.CurTime;
        _pendingCleanupRemoveBuffer.Clear();

        foreach (var (gridUid, targetTime) in _pendingCleanup)
        {

            if (HasComp<GatewayGeneratorDestinationComponent>(gridUid) || HasComp<MapperGridComponent>(gridUid))
            {
                _pendingCleanupRemoveBuffer.Add(gridUid);
                continue;
            }

            // Skip if the time hasn't elapsed yet
            if (currentTime < targetTime)
                continue;

            // Check if the entity still exists
            if (!EntityManager.EntityExists(gridUid))
            {
                _pendingCleanupRemoveBuffer.Add(gridUid);
                continue;
            }

            // Skip if this is a planet expedition grid
            if (HasComp<SalvageExpeditionComponent>(gridUid))
            {
                Logger.DebugS("salvage", $"Update: Removing expedition grid {gridUid} from cleanup queue");
                _pendingCleanupRemoveBuffer.Add(gridUid);
                continue;
            }

            // Skip if the parent map has an expedition component
            var xform = Transform(gridUid);
            var mapId = xform.MapID;
            var mapUid = _mapManager.GetMapEntityId(mapId);

            if (HasComp<SalvageExpeditionComponent>(mapUid))
            {
                Logger.DebugS("salvage", $"Update: Removing grid {gridUid} on expedition map {mapUid} from cleanup queue");
                _pendingCleanupRemoveBuffer.Add(gridUid);
                continue;
            }

            // Verify it still has a grid component
            if (!TryComp<MapGridComponent>(gridUid, out var grid))
            {
                _pendingCleanupRemoveBuffer.Add(gridUid);
                continue;
            }

            // Check tile count again to make sure it still needs to be deleted
            var tileCount = CountTiles((gridUid, grid));
            if (tileCount >= MinimumTiles)
            {
                _pendingCleanupRemoveBuffer.Add(gridUid);
                continue;
            }

            // Queue the grid for deletion
            QueueDel(gridUid);
            Logger.DebugS("salvage", $"Update: Queuing grid {gridUid} for deletion with {CountTiles((gridUid, grid))} tiles");
            _pendingCleanupRemoveBuffer.Add(gridUid);
        }

        // Remove processed grids from the pending list
        foreach (var gridUid in _pendingCleanupRemoveBuffer)
        {
            _pendingCleanup.Remove(gridUid);
        }
    }

    private int CountTiles(Entity<MapGridComponent> ent)
    {
        var grid = ent.Comp;
        var tileCount = 0;

        // Get AABB of the grid
        var aabb = grid.LocalAABB;

        // Convert to grid coordinates
        var localTL = new Vector2i((int) Math.Floor(aabb.Left), (int) Math.Floor(aabb.Bottom));
        var localBR = new Vector2i((int) Math.Ceiling(aabb.Right), (int) Math.Ceiling(aabb.Top));

        // Iterate through all tiles in the grid's area
        for (var x = localTL.X; x < localBR.X; x++)
        {
            for (var y = localTL.Y; y < localBR.Y; y++)
            {
                var position = new Vector2i(x, y);

                // Check if tile exists at position and is not empty
                var tile = grid.GetTileRef(position);
                if (!tile.Tile.IsEmpty)
                {
                    tileCount++;

                    // Early return if we've found enough tiles
                    if (tileCount >= MinimumTiles)
                        return tileCount;
                }
            }
        }

        return tileCount;
    }
}
