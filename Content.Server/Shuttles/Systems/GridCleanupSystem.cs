using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using Robust.Shared.Configuration;
using Content.Server.Gateway.Components;
using Content.Server._Lua.MapperGrid; // Lua
using Content.Server.StationEvents.Events;
using Content.Shared._Lua.Expedition;
using Content.Shared.Mind.Components;
using Content.Shared.Tiles;
using Content.Shared.Lua.CLVar; // Lua
using Robust.Shared.Player;

namespace Content.Server.Shuttles.Systems;

public sealed class GridCleanupSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly LinkedLifecycleGridSystem _linkedLifecycleGrid = default!;

    private const int MinimumTiles = 10;

    private const float CleanupDelay = 1800.0f;

    private const float EmptyGridCleanupDelay = 30.0f;

    private const int MaxDeletionsPerTick = 3;

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

        SubscribeLocalEvent<GridStartupEvent>(OnGridStartup);
        SubscribeLocalEvent<MapGridComponent, TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<ExpeditionMapComponent, ComponentStartup>(OnExpeditionStartup);
    }

    private bool IsCleanupEnabled()
    {
        return _cleanupEnabled;
    }

    private void OnGridStartup(GridStartupEvent ev)
    {
        if (TryComp<MapGridComponent>(ev.EntityUid, out var grid))
            CheckGrid((ev.EntityUid, grid));
    }

    private void OnTileChanged(Entity<MapGridComponent> ent, ref TileChangedEvent args)
    {
        CheckGrid(ent);
    }

    private void OnExpeditionStartup(EntityUid uid, ExpeditionMapComponent component, ComponentStartup args)
    {
        if (_pendingCleanup.ContainsKey(uid))
        {
            Logger.DebugS("salvage", $"Expedition startup: Removing grid {uid} from cleanup queue");
            _pendingCleanup.Remove(uid);
        }

        if (TryComp<MapGridComponent>(uid, out var grid))
        {
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

        if (IsExempt(gridUid))
        {
            _pendingCleanup.Remove(gridUid);
            return;
        }

        var tileCount = CountTiles((gridUid, grid));

        // Large enough / under construction with players — never queue.
        if (tileCount >= MinimumTiles || HasPlayersOnGrid(gridUid))
        {
            _pendingCleanup.Remove(gridUid);
            return;
        }

        var delay = tileCount == 0 ? EmptyGridCleanupDelay : CleanupDelay;
        var targetTime = _timing.CurTime + TimeSpan.FromSeconds(delay);

        if (_pendingCleanup.ContainsKey(gridUid))
        {
            _pendingCleanup[gridUid] = targetTime;
            return;
        }

        Logger.DebugS("salvage", $"CheckGrid: Scheduling grid {gridUid} for cleanup with {tileCount} tiles in {delay}s");
        _pendingCleanup[gridUid] = targetTime;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!IsCleanupEnabled() || _pendingCleanup.Count == 0)
            return;

        var currentTime = _timing.CurTime;
        _pendingCleanupRemoveBuffer.Clear();
        var deletionsThisTick = 0;

        foreach (var (gridUid, targetTime) in _pendingCleanup)
        {
            if (IsExempt(gridUid))
            {
                _pendingCleanupRemoveBuffer.Add(gridUid);
                continue;
            }

            if (currentTime < targetTime)
                continue;

            if (!EntityManager.EntityExists(gridUid))
            {
                _pendingCleanupRemoveBuffer.Add(gridUid);
                continue;
            }

            if (!TryComp<MapGridComponent>(gridUid, out var grid))
            {
                _pendingCleanupRemoveBuffer.Add(gridUid);
                continue;
            }

            var tileCount = CountTiles((gridUid, grid));
            if (tileCount >= MinimumTiles || HasPlayersOnGrid(gridUid))
            {
                _pendingCleanupRemoveBuffer.Add(gridUid);
                continue;
            }

            if (deletionsThisTick >= MaxDeletionsPerTick)
                break;

            _linkedLifecycleGrid.UnparentPlayersFromGrid(gridUid, deleteGrid: true);
            deletionsThisTick++;
            Logger.DebugS("salvage", $"Update: Deleting grid {gridUid} with {tileCount} tiles after unparenting players");
            _pendingCleanupRemoveBuffer.Add(gridUid);
        }

        foreach (var gridUid in _pendingCleanupRemoveBuffer)
        {
            _pendingCleanup.Remove(gridUid);
        }
    }

    private bool IsExempt(EntityUid gridUid)
    {
        if (HasComp<GatewayGeneratorDestinationComponent>(gridUid) || HasComp<MapperGridComponent>(gridUid))
            return true;

        if (HasComp<ExpeditionMapComponent>(gridUid) || HasComp<ExpeditionPlanetComponent>(gridUid))
            return true;

        if (!TryComp(gridUid, out TransformComponent? xform))
            return true;

        var mapUid = _mapManager.GetMapEntityId(xform.MapID);
        return HasComp<ExpeditionMapComponent>(mapUid) || HasComp<ExpeditionPlanetComponent>(mapUid);
    }
    private bool HasPlayersOnGrid(EntityUid gridUid)
    {
        var actorQuery = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (actorQuery.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid == gridUid)
                return true;
        }

        var mindQuery = EntityQueryEnumerator<MindContainerComponent, TransformComponent>();
        while (mindQuery.MoveNext(out _, out var mind, out var xform))
        {
            if (xform.GridUid == gridUid && mind.HasMind)
                return true;
        }

        return false;
    }

    private int CountTiles(Entity<MapGridComponent> ent)
    {
        var count = 0;
        foreach (var _ in _mapSystem.GetAllTiles(ent, ent.Comp))
        {
            count++;
            if (count >= MinimumTiles)
                return count;
        }

        return count;
    }
}
