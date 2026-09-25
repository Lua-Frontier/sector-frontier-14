using System.Collections.Generic;
using System.Linq;
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
    public async Task CheckTechnologyRecipesAndPrerequisites()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitPost(() =>
        {
            var research = entMan.System<ResearchSystem>();
            var technologies = protoManager.EnumeratePrototypes<TechnologyPrototype>().ToList();

            Assert.Multiple(() =>
            {
                foreach (var tech in technologies)
                {
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

                    foreach (var (faction, factionOverride) in tech.FactionOverrides)
                    {
                        if (factionOverride.TechnologyPrerequisites == null)
                            continue;

                        foreach (var prereq in factionOverride.TechnologyPrerequisites)
                        {
                            Assert.That(protoManager.TryIndex(prereq, out _), Is.True,
                                $"Technology {tech.ID} faction override {faction} has invalid prerequisite {prereq}.");
                        }

                        _ = research.GetTechnologyPrerequisites(faction, tech);
                    }
                }
            });
        });
        await server.WaitRunTicks(1);
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TechnologyUsesFactionOverridePrerequisites()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var entMan = server.ResolveDependency<IEntityManager>();
        await server.WaitAssertion(() =>
        {
            if (!protoManager.TryIndex<TechnologyPrototype>("ThrusterPiratesMachineCircuitboard", out var tech))
                return;

            var research = entMan.System<ResearchSystem>();
            var researchUid = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var researchServer = entMan.AddComponent<ResearchServerComponent>(researchUid);
            researchServer.Faction = "Pirates";
            var prereqs = research.GetTechnologyPrerequisites(researchUid, tech);
            Assert.That(prereqs, Does.Not.Contain((ProtoId<TechnologyPrototype>) "LuaDisciplinePlaceholderEngineering"));
        });
        await pair.CleanReturnAsync();
    }
}
