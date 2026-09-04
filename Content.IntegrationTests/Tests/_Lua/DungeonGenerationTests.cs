#nullable enable

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Content.Server.Procedural;
using Content.Shared._Lua.Expedition;
using Content.Shared.Procedural;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Lua;

[TestFixture]
[TestOf(typeof(DungeonSystem))]
public sealed class DungeonGenerationTests
{
    private const int Seed = 42;
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    public static IEnumerable<object[]> LuaDungeonConfigs =>
        ExpeditionDungeonPools.Shared.Select(id => new object[] { id.Id });

    /// <summary>
    /// Smoke-test: every expedition dungeon config must generate at least one room and some tiles.
    /// </summary>
    [Test]
    [TestCaseSource(nameof(LuaDungeonConfigs))]
    public async Task TestLuaDungeonGenerates(string configId)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var dungeonSys = entMan.System<DungeonSystem>();

        Assert.That(protoMan.HasIndex<DungeonConfigPrototype>(configId), Is.True,
            $"Missing dungeonConfig prototype {configId}");

        var proto = protoMan.Index<DungeonConfigPrototype>(configId);

        MapId mapId = default;
        EntityUid gridUid = default;
        MapGridComponent? grid = null;
        Task<List<Dungeon>>? genTask = null;

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out mapId);
            var gridEnt = mapManager.CreateGridEntity(mapId);
            gridUid = gridEnt.Owner;
            grid = gridEnt.Comp;
            genTask = dungeonSys.GenerateDungeonAsync(proto, proto.ID, gridUid, grid, Vector2i.Zero, Seed);
        });

        Assert.That(genTask, Is.Not.Null);

        var sw = Stopwatch.StartNew();
        while (!genTask!.IsCompleted)
        {
            await pair.RunTicksSync(5);
            Assert.That(sw.Elapsed, Is.LessThan(Timeout),
                $"Timed out generating dungeon {configId} after {sw.Elapsed}");
        }

        List<Dungeon> dungeons;
        try
        {
            dungeons = await genTask;
        }
        catch (Exception ex)
        {
            Assert.Fail($"Dungeon {configId} threw while generating: {ex}");
            await pair.CleanReturnAsync();
            return;
        }

        Assert.Multiple(() =>
        {
            Assert.That(dungeons, Is.Not.Empty, $"{configId}: expected at least one dungeon instance");
            foreach (var dungeon in dungeons)
            {
                Assert.That(dungeon.Rooms.Count, Is.GreaterThan(0),
                    $"{configId}: generated dungeon has no rooms");
                Assert.That(dungeon.AllTiles.Count, Is.GreaterThan(0),
                    $"{configId}: generated dungeon has no tiles");
            }
        });

        await server.WaitPost(() => mapSys.DeleteMap(mapId));
        await pair.CleanReturnAsync();
    }
}
