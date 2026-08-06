using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Emag.Systems;
using Content.Shared.Examine;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.Localizations;
using Content.Shared.Materials;
using Content.Shared.Research.Prototypes;
using Content.Shared.Stacks;
using JetBrains.Annotations;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Lathe;

/// <summary>
/// This handles...
/// </summary>
public abstract class SharedLatheSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedMaterialStorageSystem _materialStorage = default!;
    [Dependency] private readonly EmagSystem _emag = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] protected readonly EntityQuery<StackComponent> _stackQuery = default!;

    public readonly Dictionary<string, List<LatheRecipePrototype>> InverseRecipes = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmagLatheRecipesComponent, GotEmaggedEvent>(OnEmagged);
        SubscribeLocalEvent<EmagLatheRecipesComponent, GotUnEmaggedEvent>(OnUnemagged); // Frontier
        SubscribeLocalEvent<LatheComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        BuildInverseRecipeDictionary();
    }

    /// <summary>
    /// Get the set of all recipes that a lathe could possibly ever create (e.g., if all techs were unlocked).
    /// </summary>
    public HashSet<ProtoId<LatheRecipePrototype>> GetAllPossibleRecipes(LatheComponent component)
    {
        var recipes = new HashSet<ProtoId<LatheRecipePrototype>>();
        foreach (var pack in component.StaticPacks)
        {
            recipes.UnionWith(_proto.Index(pack).Recipes);
        }

        foreach (var pack in component.DynamicPacks)
        {
            recipes.UnionWith(_proto.Index(pack).Recipes);
        }

        return recipes;
    }

    /// <summary>
    /// Add every recipe in the list of recipe packs to a single hashset.
    /// </summary>
    public void AddRecipesFromPacks(HashSet<ProtoId<LatheRecipePrototype>> recipes, IEnumerable<ProtoId<LatheRecipePackPrototype>> packs)
    {
        foreach (var id in packs)
        {
            var pack = _proto.Index(id);
            recipes.UnionWith(pack.Recipes);
        }
    }

    private void OnExamined(Entity<LatheComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (ent.Comp.ReagentOutputSlotId != null)
            args.PushMarkup(Loc.GetString("lathe-menu-reagent-slot-examine"));

        if (ent.Comp.ProductValueModifier != null) // Frontier
            args.PushMarkup(Loc.GetString($"lathe-product-value-modifier", ("modifier", ent.Comp.ProductValueModifier))); // Frontier

    }

    [PublicAPI]
    public bool CanProduce(EntityUid uid, string recipe, int amount = 1, LatheComponent? component = null)
    {
        return _proto.TryIndex<LatheRecipePrototype>(recipe, out var proto) && CanProduce(uid, proto, amount, component);
    }

    // Mono
    public Dictionary<ProtoId<MaterialPrototype>, int> GetEndMaterialAmounts(Entity<LatheComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return new();

        var currentMaterial = _materialStorage.GetStoredMaterials(ent.Owner);
        foreach (var batch in ent.Comp.Queue)
        {
            if (!_proto.TryIndex(batch.Recipe, out var recipe))
                continue;

            foreach (var (material, needed) in recipe.Materials)
            {
                var adjustedAmount = AdjustMaterial(needed, recipe.MaterialDiscountScale, ent.Comp.FinalMaterialUseMultiplier);
                currentMaterial[material] = currentMaterial.GetValueOrDefault(material)
                    - adjustedAmount * (batch.ItemsRequested - batch.ItemsPrinted);
            }
        }
        return currentMaterial;
    }

    // Mono
    public bool CanProduceEnd(Entity<LatheComponent?> ent, LatheRecipePrototype recipe, int amount = 1)
    {
        if (!Resolve(ent, ref ent.Comp))
            return false;
        if (!HasRecipe(ent, recipe, ent.Comp))
            return false;

        var endAmts = GetEndMaterialAmounts(ent);

        foreach (var (material, needed) in recipe.Materials)
        {
            var adjustedAmount = AdjustMaterial(needed, recipe.MaterialDiscountScale, ent.Comp.FinalMaterialUseMultiplier);

            if (endAmts.GetValueOrDefault(material) < adjustedAmount * amount)
                return false;
        }
        return true;
    }

    public virtual bool CanProduce(EntityUid uid, LatheRecipePrototype recipe, int amount = 1, LatheComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;
        if (!HasRecipe(uid, recipe, component))
            return false;

        if (amount <= 0) // Frontier
            return false; // Frontier

        foreach (var (material, needed) in recipe.Materials)
        {
            var adjustedAmount = AdjustMaterial(needed, recipe.MaterialDiscountScale, component.FinalMaterialUseMultiplier); // Frontier: FinalMaterialUseMultiplier<MaterialUseMultiplier

            if (_materialStorage.GetMaterialAmount(uid, material) < adjustedAmount * amount)
                return false;
        }
        // mono start
        foreach (var (reagent, needed) in recipe.Reagents)
        {
            if (component.ReagentOutputSlotId is not { } slotId)
                return false;

            if (_container.TryGetContainer(uid, slotId, out var container) &&
                container.ContainedEntities.Count != 0 &&
                _solution.TryGetFitsInDispenser(container.ContainedEntities.First(), out _, out var solution)
                && solution.GetReagent(new ReagentId(reagent.Id, [])).Quantity < needed * amount)
                return false;
        }
        // mono end

        return true;
    }

    private void OnEmagged(EntityUid uid, EmagLatheRecipesComponent component, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        if (_emag.CheckFlag(uid, EmagType.Interaction))
            return;

        args.Handled = true;
    }

    // Frontier: demag
    private void OnUnemagged(EntityUid uid, EmagLatheRecipesComponent component, ref GotUnEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        if (!_emag.CheckFlag(uid, EmagType.Interaction))
            return;

        args.Handled = true;
    }
    // End Frontier: demag

    public static int AdjustMaterial(int original, float multScale, float multiplier)
        => (int) MathF.Ceiling(original * MathF.Pow(multiplier, multScale));

    protected abstract bool HasRecipe(EntityUid uid, LatheRecipePrototype recipe, LatheComponent component);

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs obj)
    {
        if (!obj.WasModified<LatheRecipePrototype>())
            return;
        BuildInverseRecipeDictionary();
    }

    private void BuildInverseRecipeDictionary()
    {
        InverseRecipes.Clear();
        foreach (var latheRecipe in _proto.EnumeratePrototypes<LatheRecipePrototype>())
        {
            if (latheRecipe.Result is not {} result)
                continue;

            InverseRecipes.GetOrNew(result).Add(latheRecipe);
        }
    }

    public bool TryGetRecipesFromEntity(string prototype, [NotNullWhen(true)] out List<LatheRecipePrototype>? recipes)
    {
        recipes = new();
        if (InverseRecipes.TryGetValue(prototype, out var r))
            recipes.AddRange(r);
        return recipes.Count != 0;
    }

    public string GetRecipeName(ProtoId<LatheRecipePrototype> proto)
    {
        return GetRecipeName(_proto.Index(proto));
    }

    public string GetRecipeName(LatheRecipePrototype proto)
    {
        if (!string.IsNullOrWhiteSpace(proto.Name))
            return Loc.GetString(proto.Name);

        if (proto.Result is {} result)
        {
            return _proto.Index(result).Name;
        }

        if (proto.ResultReagents is { } resultReagents)
        {
            return ContentLocalizationManager.FormatList(resultReagents
                .Select(p => Loc.GetString("lathe-menu-result-reagent-display", ("reagent", _proto.Index(p.Key).LocalizedName), ("amount", p.Value)))
                .ToList());
        }

        return string.Empty;
    }

    [PublicAPI]
    public string GetRecipeDescription(ProtoId<LatheRecipePrototype> proto)
    {
        return GetRecipeDescription(_proto.Index(proto));
    }

    public string GetRecipeDescription(LatheRecipePrototype proto)
    {
        if (!string.IsNullOrWhiteSpace(proto.Description))
            return Loc.GetString(proto.Description);

        if (proto.Result is {} result)
        {
            return _proto.Index(result).Description;
        }

        if (proto.ResultReagents is { } resultReagents)
        {
            // We only use the first one for the description since these descriptions don't combine very well.
            var reagent = resultReagents.First().Key;
            return _proto.Index(reagent).LocalizedDescription;
        }

        return string.Empty;
    }

    public void MultiplyLatheMultipliers(Entity<LatheComponent?> ent, float? materialUse = null, float? time = null)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        if (materialUse != null)
        {
            ent.Comp.MaterialUseMultiplier *= materialUse.Value;
            ent.Comp.FinalMaterialUseMultiplier *= materialUse.Value;
            Dirty(ent);
        }

        if (time != null)
        {
            ent.Comp.TimeMultiplier *= time.Value;
            ent.Comp.FinalTimeMultiplier *= time.Value;
            Dirty(ent);
        }
    }
}
