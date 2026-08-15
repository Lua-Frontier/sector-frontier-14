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
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitPost(() =>
        {
            var research = entMan.System<ResearchSystem>();
            var factions = protoManager.EnumeratePrototypes<RndFactionPrototype>()
                .Select(faction => (ProtoId<RndFactionPrototype>) faction.ID)
                .ToList();
            var technologies = protoManager.EnumeratePrototypes<TechnologyPrototype>().ToList();

            Assert.Multiple(() =>
            {
                foreach (var tech in technologies)
                {
                    Assert.That(GetDefinedPositions(tech).Any(), Is.True,
                        $"Tech {tech.ID} does not define a base position or any faction override positions.");

                    foreach (var recipe in tech.RecipeUnlocks)
                    {
                        Assert.That(protoManager.TryIndex(recipe, out _), Is.True,
                            $"Technology {tech.ID} unlocks recipe {recipe} which does not exist.");
                    }

                    foreach (var prereq in tech.TechnologyPrerequisites)
                    {
                        Assert.That(protoManager.TryIndex(prereq, out _), Is.True,
                            $"Technology {tech.ID} has {prereq} as a pre-requisite, but {prereq} is not a valid technology.");
                    }
                }

                foreach (var faction in factions)
                {
                    Dictionary<Vector2, string> techNamesByPosition = new();

                    foreach (var tech in technologies)
                    {
                        if (!research.IsTechnologyFactionAllowed(faction, tech))
                            continue;

                        var position = research.GetTechnologyPosition(faction, tech);
                        Assert.That(techNamesByPosition.TryGetValue(position, out var techName), Is.False,
                            $"Tech {tech.ID} has a duplicate position {position} with {techName} for faction {faction}.");
                        techNamesByPosition[position] = tech.ID;
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
            var tech = protoManager.Index<TechnologyPrototype>("NFAdvancedParts");
            var researchUid = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var researchServer = entMan.AddComponent<ResearchServerComponent>(researchUid);
            researchServer.Faction = "Nanotrasen";
            var ntPosition = research.GetTechnologyPosition(researchUid, tech);
            researchServer.Faction = "Syndicate";
            var syndicatePosition = research.GetTechnologyPosition(researchUid, tech);
            Assert.Multiple(() =>
            {
                Assert.That(ntPosition.X, Is.EqualTo(0f).Within(0f));
                Assert.That(ntPosition.Y, Is.EqualTo(0f).Within(0f));
                Assert.That(syndicatePosition.X, Is.EqualTo(0f).Within(0f));
                Assert.That(syndicatePosition.Y, Is.EqualTo(0f).Within(0f));
            });
        });
        await pair.CleanReturnAsync();
    }

    private static IEnumerable<Vector2> GetDefinedPositions(TechnologyPrototype tech)
    {
        if (tech.Position is { } position)
            yield return position;

        foreach (var overridePosition in tech.FactionOverrides.Values.Select(overrideData => overrideData.Position).OfType<Vector2>())
        {
            if (tech.Position == overridePosition)
                continue;

            yield return overridePosition;
        }
    }
}
