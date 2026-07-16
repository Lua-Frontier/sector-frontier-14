using Content.IntegrationTests.Tests._NF;
using Content.Server._Lua.Company;
using Content.Server._Lua.Company.Components;
using Content.Server._NF.Station.Components;
using Content.Server.Maps;
using Content.Server.Station;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._Mono.Company;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.UnitTesting.Pool;
using System.Collections.Generic;
using System.Linq;

namespace Content.IntegrationTests.Tests.Station;

[TestFixture]
[TestOf(typeof(StationJobsSystem))]
public sealed class StationJobsTest
{
    private const string StationMapId = "FooStation";

    [TestPrototypes]
    private const string Prototypes =
        "- type: playTimeTracker\n" +
        "  id: PlayTimeDummyAssistant\n\n" +
        "- type: playTimeTracker\n" +
        "  id: PlayTimeDummyMime\n\n" +
        "- type: playTimeTracker\n" +
        "  id: PlayTimeDummyClown\n\n" +
        "- type: playTimeTracker\n" +
        "  id: PlayTimeDummyCaptain\n\n" +
        "- type: playTimeTracker\n" +
        "  id: PlayTimeDummyChaplain\n\n" +
        "- type: playTimeTracker\n" +
        "  id: PlayTimeDummyNtOnly\n\n" +
        "- type: playTimeTracker\n" +
        "  id: PlayTimeDummyPirateOnly\n\n" +
        $"- type: gameMap\n" +
        $"  id: {StationMapId}\n" +
        $"  minPlayers: 0\n" +
        $"  mapName: {StationMapId}\n" +
        $"  mapPath: /Maps/Test/empty.yml\n" +
        $"  stations:\n" +
        $"    Station:\n" +
        $"      mapNameTemplate: {StationMapId}\n" +
        $"      stationProto: StandardNanotrasenStation\n" +
        $"      components:\n" +
        $"        - type: StationJobs\n" +
        $"          availableJobs:\n" +
        $"            TMime: [0, -1]\n" +
        $"            TAssistant: [-1, -1]\n" +
        $"            TCaptain: [5, 5]\n" +
        $"            TClown: [5, 6]\n" +
        $"    OwnedStation:\n" +
        $"      mapNameTemplate: OwnedStation\n" +
        $"      stationProto: StandardNanotrasenStation\n" +
        $"      components:\n" +
        $"        - type: ExtraStationInformation\n" +
        $"          requiredCompany: Nanotrasen\n" +
        $"        - type: StationJobs\n" +
        $"          availableJobs:\n" +
        $"            TNanotrasenOnly: [2, 2]\n" +
        $"            TPirateOnly: [1, 1]\n\n" +
        "- type: job\n" +
        "  id: TAssistant\n" +
        "  playTimeTracker: PlayTimeDummyAssistant\n\n" +
        "- type: job\n" +
        "  id: TMime\n" +
        "  weight: 20\n" +
        "  playTimeTracker: PlayTimeDummyMime\n\n" +
        "- type: job\n" +
        "  id: TClown\n" +
        "  weight: -10\n" +
        "  playTimeTracker: PlayTimeDummyClown\n\n" +
        "- type: job\n" +
        "  id: TCaptain\n" +
        "  weight: 10\n" +
        "  playTimeTracker: PlayTimeDummyCaptain\n\n" +
        "- type: job\n" +
        "  id: TChaplain\n" +
        "  playTimeTracker: PlayTimeDummyChaplain\n\n" +
        "- type: job\n" +
        "  id: TNanotrasenOnly\n" +
        "  playTimeTracker: PlayTimeDummyNtOnly\n" +
        "  requiredCompany: Nanotrasen\n\n" +
        "- type: job\n" +
        "  id: TPirateOnly\n" +
        "  playTimeTracker: PlayTimeDummyPirateOnly\n" +
        "  requiredCompany: Pirates\n";

    private const int StationCount = 100;
    private const int CaptainCount = StationCount;
    private const int PlayerCount = 2000;
    private const int TotalPlayers = PlayerCount + CaptainCount;

    [Test]
    public async Task AssignJobsTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var prototypeManager = server.ResolveDependency<IPrototypeManager>();
        var fooStationProto = prototypeManager.Index<GameMapPrototype>(StationMapId);
        var entSysMan = server.ResolveDependency<IEntityManager>().EntitySysManager;
        var stationJobs = entSysMan.GetEntitySystem<StationJobsSystem>();
        var stationSystem = entSysMan.GetEntitySystem<StationSystem>();
        var logmill = server.ResolveDependency<ILogManager>().RootSawmill;

        List<EntityUid> stations = new();
        await server.WaitPost(() =>
        {
            for (var i = 0; i < StationCount; i++)
            {
                stations.Add(stationSystem.InitializeNewStation(fooStationProto.Stations["Station"], null, $"Foo {StationCount}"));
            }
        });

        await server.WaitAssertion(() =>
        {
            var fakePlayers = new Dictionary<NetUserId, HumanoidCharacterProfile>()
                .AddJob("TAssistant", JobPriority.Medium, PlayerCount)
                .AddPreference("TClown", JobPriority.Low)
                .AddPreference("TMime", JobPriority.High)
                .WithPlayers(
                    new Dictionary<NetUserId, HumanoidCharacterProfile>()
                    .AddJob("TCaptain", JobPriority.High, CaptainCount)
                );
            Assert.That(fakePlayers, Is.Not.Empty);

            var start = new Stopwatch();
            start.Start();
            var assigned = stationJobs.AssignJobs(fakePlayers, stations);
            Assert.That(assigned, Is.Not.Empty);
            var time = start.Elapsed.TotalMilliseconds;
            logmill.Info($"Took {time} ms to distribute {TotalPlayers} players.");

            Assert.Multiple(() =>
            {
                foreach (var station in stations)
                {
                    var assignedHere = assigned
                        .Where(x => x.Value.Item2 == station)
                        .ToDictionary(x => x.Key, x => x.Value);

                    // Each station should have SOME players.
                    Assert.That(assignedHere, Is.Not.Empty);
                    // And it should have at least the minimum players to be considered a "fair" share, as they're all the same.
                    Assert.That(assignedHere, Has.Count.GreaterThanOrEqualTo(TotalPlayers / stations.Count), "Station has too few players.");
                    // And it shouldn't have ALL the players, either.
                    Assert.That(assignedHere, Has.Count.LessThan(TotalPlayers), "Station has too many players.");
                    // And there should be *A* captain, as there's one player with captain enabled per station.
                    Assert.That(assignedHere.Where(x => x.Value.Item1 == "TCaptain").ToList(), Has.Count.EqualTo(1));
                }

                // All clown players have assistant as a higher priority.
                Assert.That(assigned.Values.Select(x => x.Item1).ToList(), Does.Not.Contain("TClown"));
                // Mime isn't an open job-slot at round-start.
                Assert.That(assigned.Values.Select(x => x.Item1).ToList(), Does.Not.Contain("TMime"));
                // All players have slots they can fill.
                Assert.That(assigned.Values, Has.Count.EqualTo(TotalPlayers), $"Expected {TotalPlayers} players.");
                // There must be assistants present.
                Assert.That(assigned.Values.Select(x => x.Item1).ToList(), Does.Contain("TAssistant"));
                // There must be captains present, too.
                Assert.That(assigned.Values.Select(x => x.Item1).ToList(), Does.Contain("TCaptain"));
            });
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdjustJobsTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var prototypeManager = server.ResolveDependency<IPrototypeManager>();
        var fooStationProto = prototypeManager.Index<GameMapPrototype>(StationMapId);
        var entSysMan = server.ResolveDependency<IEntityManager>().EntitySysManager;
        var stationJobs = entSysMan.GetEntitySystem<StationJobsSystem>();
        var stationSystem = entSysMan.GetEntitySystem<StationSystem>();

        var station = EntityUid.Invalid;
        await server.WaitPost(() =>
        {
            station = stationSystem.InitializeNewStation(fooStationProto.Stations["Station"], null, $"Foo Station");
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            // Verify jobs are/are not unlimited.
            Assert.Multiple(() =>
            {
                Assert.That(stationJobs.IsJobUnlimited(station, "TAssistant"), "TAssistant is expected to be unlimited.");
                Assert.That(stationJobs.IsJobUnlimited(station, "TMime"), "TMime is expected to be unlimited.");
                Assert.That(!stationJobs.IsJobUnlimited(station, "TCaptain"), "TCaptain is expected to not be unlimited.");
                Assert.That(!stationJobs.IsJobUnlimited(station, "TClown"), "TClown is expected to not be unlimited.");
            });
            Assert.Multiple(() =>
            {
                Assert.That(stationJobs.TrySetJobSlot(station, "TClown", 0), "Could not set TClown to have zero slots.");
                Assert.That(stationJobs.TryGetJobSlot(station, "TClown", out var clownSlots), "Could not get the number of TClown slots.");
                Assert.That(clownSlots, Is.EqualTo(0));
                Assert.That(!stationJobs.TryAdjustJobSlot(station, "TCaptain", -9999), "Was able to adjust TCaptain by -9999 without clamping.");
                Assert.That(stationJobs.TryAdjustJobSlot(station, "TCaptain", -9999, false, true), "Could not adjust TCaptain by -9999.");
                Assert.That(stationJobs.TryGetJobSlot(station, "TCaptain", out var captainSlots), "Could not get the number of TCaptain slots.");
                Assert.That(captainSlots, Is.EqualTo(0));
            });
            Assert.Multiple(() =>
            {
                Assert.That(stationJobs.TrySetJobSlot(station, "TChaplain", 10, true), "Could not create 10 TChaplain slots.");
                stationJobs.MakeJobUnlimited(station, "TChaplain");
                Assert.That(stationJobs.IsJobUnlimited(station, "TChaplain"), "Could not make TChaplain unlimited.");
            });
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InvalidRoundstartJobsTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var prototypeManager = server.ResolveDependency<IPrototypeManager>();
        var compFact = server.ResolveDependency<IComponentFactory>();
        var name = compFact.GetComponentName<StationJobsComponent>();

        await server.WaitAssertion(() =>
        {
            // invalidJobs contains all the jobs which can't be set for preference:
            // i.e. all the jobs that shouldn't be available round-start.
            var invalidJobs = new HashSet<string>();
            foreach (var job in prototypeManager.EnumeratePrototypes<JobPrototype>())
            {
                if (!job.SetPreference)
                    invalidJobs.Add(job.ID);
            }

            Assert.Multiple(() =>
            {
                foreach (var mapProto in FrontierConstants.GameMapPrototypes) // Frontier: EnumeratePrototypes<GameMapPrototype> < FrontierConstants.GameMapPrototypes
                {
                    // Frontier: get prototype from proto ID
                    if (!prototypeManager.TryIndex<GameMapPrototype>(mapProto, out var gameMap))
                    {
                        Assert.Fail($"Could not find GameMapPrototype with ID {mapProto}! Is FrontierConstants up to date?");
                    }
                    // End Frontier

                    foreach (var (stationId, station) in gameMap.Stations)
                    {
                        if (!station.StationComponentOverrides.TryGetComponent(name, out var comp))
                            continue;

                        foreach (var (job, array) in ((StationJobsComponent) comp).SetupAvailableJobs)
                        {
                            Assert.That(array.Length, Is.EqualTo(2));
                            Assert.That(array[0] is -1 or >= 0);
                            Assert.That(array[1] is -1 or >= 0);
                            Assert.That(invalidJobs, Does.Not.Contain(job), $"Station {stationId} contains job prototype {job} which cannot be present roundstart.");
                        }
                    }
                }
            });
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StationOwnershipInitializesAfterGridAttach()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        EntityUid station = EntityUid.Invalid;
        EntityUid grid = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            var mapSystem = entMan.System<SharedMapSystem>();
            var mapManager = server.MapMan;
            var stationSystem = entMan.System<StationSystem>();

            mapSystem.CreateMap(out var mapId);
            grid = mapManager.CreateGridEntity(mapId);

            var config = new StationConfig
            {
                StationPrototype = "StandardNanotrasenStation",
                StationComponentOverrides = new ComponentRegistry
                {
                    ["ExtraStationInformation"] = new EntityPrototype.ComponentRegistryEntry(
                        new ExtraStationInformationComponent
                        {
                            RequiredCompany = "Nanotrasen"
                        },
                        null!)
                }
            };

            station = stationSystem.InitializeNewStation(config, new[] { grid }, "Ownership Test");
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            var ownedStations = entMan.System<FactionOwnedStationSystem>();

            Assert.That(entMan.TryGetComponent<CompanyComponent>(grid, out var company), Is.True);
            Assert.That(company!.CompanyName, Is.EqualTo("Nanotrasen"));

            Assert.That(entMan.TryGetComponent<FactionOwnedStationComponent>(station, out var owned), Is.True);
            Assert.That(ownedStations.TryGetCurrentOwner(station, out var owner), Is.True);
            Assert.That(owner, Is.EqualTo("Nanotrasen"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StationOwnershipChangeRebuildsCompanyRestrictedJobs()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var prototypeManager = server.ResolveDependency<IPrototypeManager>();
        var fooStationProto = prototypeManager.Index<GameMapPrototype>(StationMapId);

        EntityUid station = EntityUid.Invalid;
        EntityUid grid = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            var mapSystem = entMan.System<SharedMapSystem>();
            var mapManager = server.MapMan;
            var stationSystem = entMan.System<StationSystem>();

            mapSystem.CreateMap(out var mapId);
            grid = mapManager.CreateGridEntity(mapId);

            station = stationSystem.InitializeNewStation(fooStationProto.Stations["OwnedStation"], new[] { grid }, "Ownership Rebuild Test");
        });

        await server.WaitRunTicks(1);

        await server.WaitPost(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            var ownedStations = entMan.System<FactionOwnedStationSystem>();
            ownedStations.SetOwner(station, "Pirates");
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            var ownedStations = entMan.System<FactionOwnedStationSystem>();
            var stationJobs = entMan.System<StationJobsSystem>();

            Assert.Multiple(() =>
            {
                Assert.That(ownedStations.TryGetCurrentOwner(station, out var currentOwner), Is.True);
                Assert.That(currentOwner, Is.EqualTo("Pirates"));
                Assert.That(ownedStations.GetSpawnAccessCompanies(station), Is.EqualTo("Pirates"));

                Assert.That(entMan.TryGetComponent<CompanyComponent>(grid, out var company), Is.True);
                Assert.That(company!.CompanyName, Is.EqualTo("Pirates"));

                Assert.That(stationJobs.TryGetJobSlot(station, "TNanotrasenOnly", out var ntSlots), Is.True);
                Assert.That(ntSlots, Is.EqualTo(0));

                Assert.That(stationJobs.TryGetJobSlot(station, "TPirateOnly", out var pirateSlots), Is.True);
                Assert.That(pirateSlots, Is.EqualTo(1));
            });
        });

        await pair.CleanReturnAsync();
    }
}

internal static class JobExtensions
{
    public static Dictionary<NetUserId, HumanoidCharacterProfile> AddJob(
        this Dictionary<NetUserId, HumanoidCharacterProfile> inp, string jobId, JobPriority prio = JobPriority.Medium,
        int amount = 1)
    {
        for (var i = 0; i < amount; i++)
        {
            inp.Add(new NetUserId(Guid.NewGuid()), HumanoidCharacterProfile.Random().WithJobPriority(jobId, prio));
        }

        return inp;
    }

    public static Dictionary<NetUserId, HumanoidCharacterProfile> AddPreference(
        this Dictionary<NetUserId, HumanoidCharacterProfile> inp, string jobId, JobPriority prio = JobPriority.Medium)
    {
        return inp.ToDictionary(x => x.Key, x => x.Value.WithJobPriority(jobId, prio));
    }

    public static Dictionary<NetUserId, HumanoidCharacterProfile> WithPlayers(
        this Dictionary<NetUserId, HumanoidCharacterProfile> inp,
        Dictionary<NetUserId, HumanoidCharacterProfile> second)
    {
        return new[] { inp, second }.SelectMany(x => x).ToDictionary(x => x.Key, x => x.Value);
    }
}
