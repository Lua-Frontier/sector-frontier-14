using Content.Server.Worldgen.Components;
using Robust.Server.GameObjects;
using Content.Server._NF.Worldgen.Components.Debris; // Frontier
using Content.Server._NF.Salvage; // Frontier
using Content.Server.StationEvents.Events; // Frontier
using Robust.Shared.Spawners;

namespace Content.Server.Worldgen.Systems;

/// <summary>
///     This handles loading in objects based on distance from player, using some metadata on chunks.
/// </summary>
public sealed class LocalityLoaderSystem : BaseWorldSystem
{
    [Dependency] private readonly TransformSystem _xformSys = default!;
    [Dependency] private readonly LinkedLifecycleGridSystem _linkedLifecycleGrid = default!; // Frontier

    // Duration to reset the despawn timer to when a debris is loaded into a player's view.
    private const float DebrisActiveDuration = 3600; //20 минут

    // Frontier: space debris destruction
    public override void Initialize()
    {
        SubscribeLocalEvent<SpaceDebrisComponent, EntityTerminatingEvent>(OnDebrisDespawn);
    }
    // End Frontier: space debris destruction

    /// <inheritdoc />
    public override void Update(float frameTime)
    {
        var e = EntityQueryEnumerator<LocalityLoaderComponent, TransformComponent>();
        var loadedQuery = GetEntityQuery<LoadedChunkComponent>();
        var xformQuery = GetEntityQuery<TransformComponent>();
        var controllerQuery = GetEntityQuery<WorldControllerComponent>();

        while (e.MoveNext(out var uid, out var loadable, out var xform))
        {
            if (!controllerQuery.TryGetComponent(xform.MapUid, out var controller))
            {
                RaiseLocalEvent(uid, new LocalStructureLoadedEvent());
                RemCompDeferred<LocalityLoaderComponent>(uid);
                continue;
            }

            var coords = GetChunkCoords(uid, xform);
            var done = false;
            for (var i = -1; i < 2 && !done; i++)
            {
                for (var j = -1; j < 2 && !done; j++)
                {
                    if (!controller.Chunks.TryGetValue(coords + (i, j), out var chunk))
                        continue;

                    if (!loadedQuery.TryGetComponent(chunk, out var loaded) || loaded.Loaders is null)
                        continue;

                    foreach (var loader in loaded.Loaders)
                    {
                        if (!xformQuery.TryGetComponent(loader, out var loaderXform))
                            continue;

                        if ((_xformSys.GetWorldPosition(loaderXform) - _xformSys.GetWorldPosition(xform)).Length() > loadable.LoadingDistance)
                            continue;

                        // Reset the TimedDespawnComponent's lifetime when loaded
                        ResetTimedDespawn(uid);

                        RaiseLocalEvent(uid, new LocalStructureLoadedEvent());
                        RemCompDeferred<LocalityLoaderComponent>(uid);
                        done = true;
                        break;
                    }
                }
            }
        }
    }
    private void ResetTimedDespawn(EntityUid uid)
    {
        if (TryComp<TimedDespawnComponent>(uid, out var timedDespawn))
        {
            timedDespawn.Lifetime = DebrisActiveDuration;
        }
        else
        {
            // Add TimedDespawnComponent if it does not exist
            timedDespawn = AddComp<TimedDespawnComponent>(uid);
            timedDespawn.Lifetime = DebrisActiveDuration;
        }
    }

    // Frontier
    private void OnDebrisDespawn(EntityUid entity, SpaceDebrisComponent component, EntityTerminatingEvent e)
    {
        // Handle mobrestrictions getting deleted
        var query = AllEntityQuery<NFSalvageMobRestrictionsComponent>();

        while (query.MoveNext(out var salvUid, out var salvMob))
        {
            if (entity == salvMob.LinkedGridEntity)
                QueueDel(salvUid);
        }

        // Do not delete the grid, it is being deleted.
        _linkedLifecycleGrid.UnparentPlayersFromGrid(grid: entity, deleteGrid: false, ignoreLifeStage: true);
    }
    // End Frontier
}

/// <summary>
///     A directed fired on a loadable entity when a local loader enters it's vicinity.
/// </summary>
public record struct LocalStructureLoadedEvent;
