using System.Numerics;
using Content.Server.GameTicking;
using Content.Server.Spawners.Components;
using Content.Shared.EntityTable;
using Content.Shared.GameTicking.Components;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Content.Server.Anomaly.Systems;

namespace Content.Server.Spawners.EntitySystems
{
    [UsedImplicitly]
    public sealed class ConditionalSpawnerSystem : EntitySystem
    {
        [Dependency] private readonly IRobustRandom _robustRandom = default!;
        [Dependency] private readonly GameTicker _ticker = default!;
        [Dependency] private readonly EntityTableSystem _entityTable = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<GameRuleStartedEvent>(OnRuleStarted);
            SubscribeLocalEvent<ConditionalSpawnerComponent, MapInitEvent>(OnCondSpawnMapInit);
            SubscribeLocalEvent<RandomSpawnerComponent, MapInitEvent>(OnRandSpawnMapInit);
            SubscribeLocalEvent<EntityTableSpawnerComponent, MapInitEvent>(OnEntityTableSpawnMapInit);
        }

        private void OnCondSpawnMapInit(EntityUid uid, ConditionalSpawnerComponent component, MapInitEvent args)
        {
            TrySpawn(uid, component);
        }

        private void OnRandSpawnMapInit(EntityUid uid, RandomSpawnerComponent component, MapInitEvent args)
        {
            RandomLoggerWrapper.LogGlobalRandomCall($"RandomSpawner spawning entity at {Transform(uid).Coordinates}");

            Spawn(uid, component);
            if (component.DeleteSpawnerAfterSpawn)
                QueueDel(uid);
        }

        private void OnEntityTableSpawnMapInit(Entity<EntityTableSpawnerComponent> ent, ref MapInitEvent args)
        {
            Spawn(ent);
            if (ent.Comp.DeleteSpawnerAfterSpawn && !TerminatingOrDeleted(ent) && Exists(ent))
                QueueDel(ent);
        }

        private void OnRuleStarted(ref GameRuleStartedEvent args)
        {
            var query = EntityQueryEnumerator<ConditionalSpawnerComponent>();
            while (query.MoveNext(out var uid, out var component))
            {
                if (component.GameRules.Contains(args.RuleId))
                {
                    TrySpawn(uid, component);
                }
            }
        }

        private void TrySpawn(EntityUid uid, ConditionalSpawnerComponent component)
        {
            Spawn(uid, component);
        }

        private void Spawn(EntityUid uid, ConditionalSpawnerComponent component)
        {
            if (component.Prototypes.Count == 0)
            {
                Log.Warning($"Prototype list in ConditionalSpawnerComponent is empty! Entity: {ToPrettyString(uid)}");
                return;
            }

            if (Deleted(uid))
                return;

            var coordinates = Transform(uid).Coordinates;

            EntityManager.SpawnEntity(_robustRandom.Pick(component.Prototypes), coordinates);

            RandomLoggerWrapper.LogGlobalRandomCall($"ConditionalSpawner: Pick(Prototypes[{component.Prototypes.Count}])");
        }

        private void Spawn(EntityUid uid, RandomSpawnerComponent component)
        {
            if (component.RarePrototypes.Count > 0 && (component.RareChance == 1.0f || _robustRandom.Prob(component.RareChance)))
            {
                RandomLoggerWrapper.LogGlobalRandomCall($"RandomSpawner: Prob({component.RareChance}) = {_robustRandom.Prob(component.RareChance)}");

                var rarePrototype = _robustRandom.Pick(component.RarePrototypes);
                RandomLoggerWrapper.LogGlobalRandomCall($"RandomSpawner: Pick(RarePrototypes[{component.RarePrototypes.Count}]) = {rarePrototype}");

                EntityManager.SpawnEntity(rarePrototype, Transform(uid).Coordinates);
                return;
            }

            if (component.Chance != 1.0f && !_robustRandom.Prob(component.Chance))
            {
                RandomLoggerWrapper.LogGlobalRandomCall($"RandomSpawner: Prob({component.Chance}) = {_robustRandom.Prob(component.Chance)} - SPAWN BLOCKED");
                return;
            }

            if (component.Prototypes.Count == 0)
            {
                Log.Warning($"Prototype list in RandomSpawnerComponent is empty! Entity: {ToPrettyString(uid)}");
                return;
            }

            if (Deleted(uid))
                return;

            var offset = component.Offset;
            var xOffset = _robustRandom.NextFloat(-offset, offset);
            var yOffset = _robustRandom.NextFloat(-offset, offset);

            RandomLoggerWrapper.LogGlobalRandomCall($"RandomSpawner: NextFloat(-{offset}, {offset}) = {xOffset:F4} for X offset");
            RandomLoggerWrapper.LogGlobalRandomCall($"RandomSpawner: NextFloat(-{offset}, {offset}) = {yOffset:F4} for Y offset");

            var coordinates = Transform(uid).Coordinates.Offset(new Vector2(xOffset, yOffset));

            var prototype = _robustRandom.Pick(component.Prototypes);
            RandomLoggerWrapper.LogGlobalRandomCall($"RandomSpawner: Pick(Prototypes[{component.Prototypes.Count}]) = {prototype}");

            EntityManager.SpawnEntity(prototype, coordinates);
        }

        private void Spawn(Entity<EntityTableSpawnerComponent> ent)
        {
            if (TerminatingOrDeleted(ent) || !Exists(ent))
                return;

            var coords = Transform(ent).Coordinates;

            var spawns = _entityTable.GetSpawns(ent.Comp.Table);
            foreach (var proto in spawns)
            {
                var xOffset = _robustRandom.NextFloat(-ent.Comp.Offset, ent.Comp.Offset);
                var yOffset = _robustRandom.NextFloat(-ent.Comp.Offset, ent.Comp.Offset);

                RandomLoggerWrapper.LogGlobalRandomCall($"EntityTableSpawner: NextFloat(-{ent.Comp.Offset}, {ent.Comp.Offset}) = {xOffset:F4} for X offset");
                RandomLoggerWrapper.LogGlobalRandomCall($"EntityTableSpawner: NextFloat(-{ent.Comp.Offset}, {ent.Comp.Offset}) = {yOffset:F4} for Y offset");

                var trueCoords = coords.Offset(new Vector2(xOffset, yOffset));

                SpawnAtPosition(proto, trueCoords);
            }
        }
    }
}
