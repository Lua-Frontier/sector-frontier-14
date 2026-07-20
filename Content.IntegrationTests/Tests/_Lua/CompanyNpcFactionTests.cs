#nullable enable

using Content.Server._Mono.Company;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Lua;

[TestFixture]
public sealed class CompanyNpcFactionTests
{
    private static readonly ProtoId<NpcFactionPrototype> NeutralFaction = "Neutral";
    private static readonly ProtoId<NpcFactionPrototype> NanoTrasenFaction = "NanoTrasen";
    private static readonly ProtoId<NpcFactionPrototype> PirateFaction = "NFPirate";
    private static readonly ProtoId<NpcFactionPrototype> SyndicateFaction = "Syndicate";
    private static readonly ProtoId<NpcFactionPrototype> SimpleHostileFaction = "SimpleHostile";

    [Test]
    public async Task CompanyChangePreservesBaseNpcFactions()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        EntityUid player = EntityUid.Invalid;
        bool hasNeutral = false;
        bool hostileTargetsNeutral = false;
        bool hasStormNeutral = false;
        bool stormStillHostile = false;
        bool hasSyndicateNeutral = false;
        bool hasSyndicateFaction = false;
        bool syndicateStillHostile = false;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            player = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            var factionComp = entMan.EnsureComponent<NpcFactionMemberComponent>(player);
            var npcFaction = server.System<NpcFactionSystem>();
            npcFaction.AddFaction(new Entity<NpcFactionMemberComponent?>(player, factionComp), NeutralFaction);
        });

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var npcFaction = server.System<NpcFactionSystem>();
            var factionComp = entMan.GetComponent<NpcFactionMemberComponent>(player);
            var entity = new Entity<NpcFactionMemberComponent?>(player, factionComp);

            hasNeutral = npcFaction.IsMember(entity, NeutralFaction);
            hostileTargetsNeutral = npcFaction.IsFactionHostile(SimpleHostileFaction, entity);
        });

        Assert.That(hasNeutral, Is.True);
        Assert.That(hostileTargetsNeutral, Is.True);

        await server.WaitPost(() =>
        {
            var company = server.System<CompanySystem>();
            company.SetCompany(player, "StormCreed");
        });

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var npcFaction = server.System<NpcFactionSystem>();
            var factionComp = entMan.GetComponent<NpcFactionMemberComponent>(player);
            var entity = new Entity<NpcFactionMemberComponent?>(player, factionComp);

            hasStormNeutral = npcFaction.IsMember(entity, NeutralFaction);
            stormStillHostile = npcFaction.IsFactionHostile(SimpleHostileFaction, entity);
        });

        Assert.That(hasStormNeutral, Is.True,
            "StormCreed must not clear the player's baseline Neutral NPC faction.");
        Assert.That(stormStillHostile, Is.True,
            "SimpleHostile NPCs must still consider the player hostile after joining StormCreed.");

        await server.WaitPost(() =>
        {
            var company = server.System<CompanySystem>();
            company.SetCompany(player, "Syndicate");
        });

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var npcFaction = server.System<NpcFactionSystem>();
            var factionComp = entMan.GetComponent<NpcFactionMemberComponent>(player);
            var entity = new Entity<NpcFactionMemberComponent?>(player, factionComp);

            hasSyndicateNeutral = npcFaction.IsMember(entity, NeutralFaction);
            hasSyndicateFaction = npcFaction.IsMember(entity, SyndicateFaction);
            syndicateStillHostile = npcFaction.IsFactionHostile(SimpleHostileFaction, entity);
        });

        Assert.That(hasSyndicateNeutral, Is.True,
            "Changing to a company with npcFactions must preserve the player's baseline faction.");
        Assert.That(hasSyndicateFaction, Is.True,
            "Syndicate company should layer its NPC faction on top of the player's baseline faction.");
        Assert.That(syndicateStillHostile, Is.True);

        await server.WaitRunTicks(1);
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CompanyChangeReplacesOldCompanyNpcFactions()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        EntityUid player = EntityUid.Invalid;
        bool hasPirateFaction = false;
        bool hasNeutralFaction = false;
        bool stillHasPirateFaction = false;
        bool hasNanotrasenFaction = false;
        bool stillHasNeutralFaction = false;
        bool pirateSurvivedNanotrasen = false;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            player = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            var company = server.System<CompanySystem>();
            company.SetCompany(player, "Pirates");
        });

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var npcFaction = server.System<NpcFactionSystem>();
            var factionComp = entMan.GetComponent<NpcFactionMemberComponent>(player);
            var entity = new Entity<NpcFactionMemberComponent?>(player, factionComp);

            hasPirateFaction = npcFaction.IsMember(entity, PirateFaction);
        });

        Assert.That(hasPirateFaction, Is.True,
            "Pirates company should apply the NFPirate NPC faction.");

        await server.WaitPost(() =>
        {
            var company = server.System<CompanySystem>();
            company.SetCompany(player, "Neutral");
        });

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var npcFaction = server.System<NpcFactionSystem>();
            var factionComp = entMan.GetComponent<NpcFactionMemberComponent>(player);
            var entity = new Entity<NpcFactionMemberComponent?>(player, factionComp);

            hasNeutralFaction = npcFaction.IsMember(entity, NeutralFaction);
            stillHasPirateFaction = npcFaction.IsMember(entity, PirateFaction);
        });

        Assert.That(hasNeutralFaction, Is.True,
            "Switching to Neutral should apply only the Neutral NPC faction.");
        Assert.That(stillHasPirateFaction, Is.False,
            "Leaving Pirates must remove the old NFPirate NPC faction.");

        await server.WaitPost(() =>
        {
            var company = server.System<CompanySystem>();
            company.SetCompany(player, "Nanotrasen");
        });

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var npcFaction = server.System<NpcFactionSystem>();
            var factionComp = entMan.GetComponent<NpcFactionMemberComponent>(player);
            var entity = new Entity<NpcFactionMemberComponent?>(player, factionComp);

            hasNanotrasenFaction = npcFaction.IsMember(entity, NanoTrasenFaction);
            stillHasNeutralFaction = npcFaction.IsMember(entity, NeutralFaction);
            pirateSurvivedNanotrasen = npcFaction.IsMember(entity, PirateFaction);
        });

        Assert.That(hasNanotrasenFaction, Is.True,
            "Nanotrasen company should apply the NanoTrasen NPC faction.");
        Assert.That(stillHasNeutralFaction, Is.False,
            "Switching away from Neutral must remove the old Neutral NPC faction.");
        Assert.That(pirateSurvivedNanotrasen, Is.False,
            "An older Pirates company faction must not survive later company changes.");

        await server.WaitRunTicks(1);
        await pair.CleanReturnAsync();
    }
}