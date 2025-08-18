using Content.Server.Spawners.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Server.Anomaly.Systems;

namespace Content.Server.Spawners.EntitySystems;

public sealed class SpawnerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TimedSpawnerComponent, MapInitEvent>(OnMapInit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<TimedSpawnerComponent>();
        while (query.MoveNext(out var uid, out var timedSpawner))
        {
            if (timedSpawner.NextFire > curTime)
                continue;

            OnTimerFired(uid, timedSpawner);

            timedSpawner.NextFire += timedSpawner.IntervalSeconds;
        }
    }

    private void OnMapInit(Entity<TimedSpawnerComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextFire = _timing.CurTime + ent.Comp.IntervalSeconds;
    }

    private void OnTimerFired(EntityUid uid, TimedSpawnerComponent component)
    {
        var probResult = _random.Prob(component.Chance);
        RandomLoggerWrapper.LogGlobalRandomCall($"TimedSpawner: Prob({component.Chance}) = {probResult}");

        if (!probResult)
            return;

        var number = _random.Next(component.MinimumEntitiesSpawned, component.MaximumEntitiesSpawned);
        RandomLoggerWrapper.LogGlobalRandomCall($"TimedSpawner: Next({component.MinimumEntitiesSpawned}, {component.MaximumEntitiesSpawned}) = {number}");

        var coordinates = Transform(uid).Coordinates;

        for (var i = 0; i < number; i++)
        {
            var entity = _random.Pick(component.Prototypes);
            RandomLoggerWrapper.LogGlobalRandomCall($"TimedSpawner: Pick(Prototypes[{component.Prototypes.Count}]) = {entity}");

            SpawnAtPosition(entity, coordinates);
        }
    }
}
