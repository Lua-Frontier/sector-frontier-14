using Content.Server.Atmos.Components;
using Content.Server._NF.Worldgen.Components.Debris;
using Content.Server.Shuttles.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Events;

namespace Content.Server.Atmos.EntitySystems;

/// <summary>
/// Handles automatically adding a grid atmosphere to grids that become large enough, allowing players to build shuttles
/// with a sealed atmosphere from scratch.
/// </summary>
public sealed class AutomaticAtmosSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    private const int MinimumTilesForAtmosphere = 16;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MapGridComponent, MassDataChangedEvent>(OnMassDataChanged);
    }

    private void OnMassDataChanged(Entity<MapGridComponent> ent, ref MassDataChangedEvent ev)
    {
        if (_atmosphereSystem.HasAtmosphere(ent))
            return;

        if (HasComp<SpaceDebrisComponent>(ent))
            return;
        var estimatedTiles = ev.NewMass / ShuttleSystem.TileDensityMultiplier;
        if (estimatedTiles < MinimumTilesForAtmosphere)
            return;

        if (CountNonEmptyTiles(ent, MinimumTilesForAtmosphere) < MinimumTilesForAtmosphere)
            return;

        AddComp<GridAtmosphereComponent>(ent);
        Log.Info($"Giving grid {ent} GridAtmosphereComponent.");
    }

    private int CountNonEmptyTiles(Entity<MapGridComponent> ent, int stopAt)
    {
        var count = 0;
        var enumerator = _map.GetAllTilesEnumerator(ent, ent.Comp);
        while (enumerator.MoveNext(out _))
        {
            count++;
            if (count >= stopAt)
                return count;
        }

        return count;
    }
}
