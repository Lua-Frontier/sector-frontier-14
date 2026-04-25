using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server.Research.Systems;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._NF;

[TestFixture]
public sealed class TechnologyTreeTests
{
    [Test]
    public async Task CheckDuplicateTechPositions()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var protoManager = server.ResolveDependency<IPrototypeManager>();

        Dictionary<Vector2, string> techNamesByPosition = new();

        await server.WaitPost(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var tech in protoManager.EnumeratePrototypes<TechnologyPrototype>())
                {
                    var positions = GetDefinedPositions(tech).ToList();
                    Assert.That(positions.Count, Is.GreaterThan(0), $"Tech {tech.ID} does not define a base position or any faction override positions.");

                    foreach (var position in positions)
                    {
                        Assert.That(techNamesByPosition.TryGetValue(position, out var techName), Is.False, $"Tech {tech.ID} has a duplicate position {position} with {techName}.");
                        techNamesByPosition[position] = tech.ID;
                    }

                    foreach (var recipe in tech.RecipeUnlocks)
                    {
                        Assert.That(protoManager.TryIndex(recipe, out var proto), Is.True, $"Technology {tech.ID} unlocks recipe {recipe} which does not exist.");
                    }

                    foreach (var prereq in tech.TechnologyPrerequisites)
                    {
                        Assert.That(protoManager.TryIndex(prereq, out var proto), Is.True, $"Technology {tech.ID} has {prereq} as a pre-requisite, but {prereq} is not a valid technology.");
                    }
                }
            });
        });
        await server.WaitRunTicks(1);
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TechnologyUsesFactionOverridePosition()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var entMan = server.ResolveDependency<IEntityManager>();
        await server.WaitAssertion(() =>
        {
            var research = entMan.System<ResearchSystem>();
            var tech = protoManager.Index<TechnologyPrototype>("LuaStarGateShuttleBeacon");
            var researchUid = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var researchServer = entMan.AddComponent<ResearchServerComponent>(researchUid);
            researchServer.Faction = "Nanotrasen";
            var ntPosition = research.GetTechnologyPosition(researchUid, tech);
            researchServer.Faction = "Syndicate";
            var syndicatePosition = research.GetTechnologyPosition(researchUid, tech);
            Assert.Multiple(() =>
            {
                Assert.That(ntPosition.X, Is.EqualTo(-14.152f).Within(0.001f));
                Assert.That(ntPosition.Y, Is.EqualTo(1.013f).Within(0.001f));
                Assert.That(syndicatePosition.X, Is.EqualTo(-14.255f).Within(0.001f));
                Assert.That(syndicatePosition.Y, Is.EqualTo(1f).Within(0.001f));
            });
        });
        await pair.CleanReturnAsync();
    }

    private static IEnumerable<Vector2> GetDefinedPositions(TechnologyPrototype tech)
    {
        if (tech.Position is { } position) yield return position;
        foreach (var overridePosition in tech.FactionOverrides.Values.Select(overrideData => overrideData.Position).OfType<Vector2>())
        {
            if (tech.Position == overridePosition) continue;
            yield return overridePosition;
        }
    }
}
