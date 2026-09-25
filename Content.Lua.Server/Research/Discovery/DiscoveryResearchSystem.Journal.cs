using System.Linq;
using Content.Lua.Shared.Research.Discovery;
using Content.Shared.Materials;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.UserInterface;
using Robust.Shared.Prototypes;
using Content.Shared.Research.Discovery;

namespace Content.Lua.Server.Research.Discovery;

public sealed partial class DiscoveryResearchSystem
{
    private void OnJournalUiOpen(EntityUid uid, DiscoveryJournalConsoleComponent comp, AfterActivatableUIOpenEvent args)
    {
        RefreshJournalUi(uid);
    }

    private void OnJournalResearchRegistrationChanged(EntityUid uid, DiscoveryJournalConsoleComponent comp, ref ResearchRegistrationChangedEvent args)
    {
        RefreshJournalUi(uid);
    }

    private void RefreshJournalUi(EntityUid uid)
    {
        _ui.SetUiState(uid, DiscoveryJournalUiKey.Key, BuildJournalState(uid));
    }

    private DiscoveryJournalBoundUserInterfaceState BuildJournalState(EntityUid uid)
    {
        var state = new DiscoveryJournalBoundUserInterfaceState
        {
            WorkingComputeHint = CountPoweredWorkingCompute(uid),
        };

        if (TryComp(uid, out DiscoveryJournalConsoleComponent? journal))
        {
            state.Printing = journal.Printing;
            state.PrintProgress = journal.PrintProgress;
            state.PrintDuration = journal.PrintDuration;
        }

        if (!TryGetClientServer(uid, out _, out var server, out var db))
            return state;

        state.HasServer = true;
        state.Points = server.Points;
        state.Faction = server.Faction;

        FactionResearchProfilePrototype? profile = null;
        PrototypeManager.TryIndex(new ProtoId<FactionResearchProfilePrototype>((string)server.Faction), out profile);

        foreach (var techId in db.UnlockedTechnologies)
        {
            if (!PrototypeManager.TryIndex(techId, out TechnologyPrototype? tech))
                continue;

            string? disciplineName = tech.Discipline;
            var disciplineColor = Color.FromHex("#7B68A6");
            if (PrototypeManager.TryIndex(tech.Discipline, out TechDisciplinePrototype? disc))
            {
                disciplineName = Loc.GetString(disc.Name);
                disciplineColor = disc.Color;
            }

            string? entityIcon = null;
            if (tech.EntityIcon is { } icon)
            {
                if (PrototypeManager.HasIndex(icon))
                    entityIcon = icon;
                else if (PrototypeManager.TryIndex<LatheRecipePrototype>(icon.Id, out var asRecipe) &&
                         asRecipe.Result is { } recipeResult)
                    entityIcon = recipeResult;
            }

            if (entityIcon == null)
            {
                foreach (var unlock in tech.RecipeUnlocks)
                {
                    if (!PrototypeManager.TryIndex(unlock, out LatheRecipePrototype? unlockRecipe))
                        continue;
                    if (unlockRecipe.Result is not { } result)
                        continue;
                    entityIcon = result;
                    break;
                }
            }

            var recipeNames = new List<string>();
            foreach (var recipeId in tech.RecipeUnlocks)
            {
                recipeNames.Add(RecipeDisplayName(recipeId));
            }

            state.Unlocked.Add(new DiscoveryUnlockedEntry
            {
                Id = tech.ID,
                Name = TechnologyDisplayName(tech),
                Discipline = tech.Discipline,
                DisciplineName = disciplineName,
                DisciplineColor = disciplineColor,
                EntityIcon = entityIcon,
                Cost = tech.Cost,
                Tier = 0,
                RecipeIds = tech.RecipeUnlocks.Select(r => (string)r).ToList(),
                RecipeNames = recipeNames,
                IsSignature = profile != null &&
                              (profile.SignatureTechs.Contains(tech.ID) || tech.SignatureFor == server.Faction),
            });
        }

        state.Unlocked = state.Unlocked.OrderBy(e => e.Discipline).ThenBy(e => e.Name).ToList();
        return state;
    }

    private void OnPrintBlueprint(EntityUid uid, DiscoveryJournalConsoleComponent comp, DiscoveryPrintBlueprintMessage args)
    {
        if (comp.Printing)
        {
            _popup.PopupEntity(Loc.GetString("discovery-research-busy"), uid, args.Actor);
            return;
        }

        if (!TryGetClientServer(uid, out _, out _, out var db))
        {
            _popup.PopupEntity(Loc.GetString("discovery-research-need-server"), uid, args.Actor);
            return;
        }

        if (!PrototypeManager.TryIndex(args.RecipeId, out LatheRecipePrototype? _))
            return;

        var allowed = false;
        foreach (var techId in db.UnlockedTechnologies)
        {
            if (!PrototypeManager.TryIndex(techId, out TechnologyPrototype? tech))
                continue;
            if (tech.RecipeUnlocks.Any(r => r == args.RecipeId))
            {
                allowed = true;
                break;
            }
        }

        if (!allowed)
        {
            _popup.PopupEntity(Loc.GetString("discovery-research-blueprint-not-unlocked"), uid, args.Actor);
            return;
        }

        const int paperNeeded = 100;
        if (!TryComp(uid, out MaterialStorageComponent? storage) ||
            !_material.TryChangeMaterialAmount(uid, DiscoveryResearchConstants.PaperMaterial, -paperNeeded, storage))
        {
            _popup.PopupEntity(Loc.GetString("discovery-research-need-paper"), uid, args.Actor);
            return;
        }

        comp.PendingBlueprintRecipe = args.RecipeId;
        comp.Printing = true;
        comp.PrintProgress = 0f;
        comp.PrintDuration = (float)DiscoveryResearchConstants.BlueprintPrintTime.TotalSeconds;
        Dirty(uid, comp);
        RefreshJournalUi(uid);
    }

    private void UpdateBlueprintPrints(float frameTime)
    {
        var query = EntityQueryEnumerator<DiscoveryJournalConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Printing)
                continue;

            comp.PrintProgress += frameTime;
            Dirty(uid, comp);

            if (comp.PrintProgress < comp.PrintDuration)
            {
                if (_ui.IsUiOpen(uid, DiscoveryJournalUiKey.Key))
                    RefreshJournalUi(uid);
                continue;
            }

            CompleteBlueprintPrint(uid, comp);
        }
    }

    private void CompleteBlueprintPrint(EntityUid uid, DiscoveryJournalConsoleComponent comp)
    {
        var recipeId = comp.PendingBlueprintRecipe;
        comp.Printing = false;
        comp.PrintProgress = 0f;
        comp.PendingBlueprintRecipe = null;
        Dirty(uid, comp);

        if (string.IsNullOrEmpty(recipeId))
        {
            RefreshJournalUi(uid);
            return;
        }

        var blueprint = Spawn("ResearchRecipeBlueprint", Transform(uid).Coordinates);
        _blueprintLathe.SetBlueprintRecipes((blueprint, Comp<BlueprintComponent>(blueprint)),
            new HashSet<ProtoId<LatheRecipePrototype>> { recipeId });
        _meta.SetEntityName(blueprint, Loc.GetString("discovery-research-blueprint-name", ("recipe", RecipeDisplayName(recipeId))));

        _popup.PopupEntity(Loc.GetString("discovery-research-blueprint-printed"), uid);
        RefreshJournalUi(uid);
    }
}
