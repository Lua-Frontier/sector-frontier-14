//REV
using Content.Server.Procedural;
using Content.Shared.Maps;
using Content.Shared.Procedural;
using Content.Shared.Random.Helpers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Lua.FTLPoints.Effects;

[DataDefinition]
public sealed partial class SpawnDungeonEffect : FtlPointEffect
{
    [DataField("configPrototypes")]
    public List<string> ConfigPrototypes { set; get; } = new List<string>()
    {
        "Experiment",
        "LavaBrig",
    };

    [DataField("minSpawn")] public int MinSpawn = 1;
    [DataField("maxSpawn")] public int MaxSpawn = 2;
    [DataField("range")] public int SpawnRange = 200;

    public override void Effect(FtlPointEffectArgs args)
    {
        var random = IoCManager.Resolve<IRobustRandom>();
        var amountToSpawn = random.Next(MinSpawn, MaxSpawn);

        for (int i = 0; i < amountToSpawn; i++)
        {
            var dungeon = args.EntityManager.System<DungeonSystem>();
            var prototype = IoCManager.Resolve<IPrototypeManager>();

            var position = new Vector2i(random.Next(-SpawnRange, SpawnRange), random.Next(-SpawnRange, SpawnRange));
            var dungeonUid = args.MapManager.GetMapEntityId(args.MapId);

            if (!args.EntityManager.TryGetComponent<MapGridComponent>(dungeonUid, out var dungeonGrid))
            {
                dungeonUid = args.EntityManager.CreateEntityUninitialized(null, new EntityCoordinates(dungeonUid, position));
                dungeonGrid = args.EntityManager.EnsureComponent<MapGridComponent>(dungeonUid);
                args.EntityManager.InitializeAndStartEntity(dungeonUid, args.MapId);
            }

            var seed = new Random().Next();

            if (!prototype.TryIndex<DungeonConfigPrototype>(random.Pick(ConfigPrototypes), out var dungeonProto))
            {
                return;
            }

            if (!args.EntityManager.TryGetComponent<MapGridComponent>(dungeonUid, out var _))
            {
                Logger.Warning($"Dungeon {dungeonUid} did not have a MapGridComponent.");
                return;
            }

            dungeon.GenerateDungeon(dungeonProto, "", dungeonUid, dungeonGrid, position, seed);
        }
    }
}
