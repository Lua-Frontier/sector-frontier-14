using Content.Shared.Lathe.Prototypes;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Lathe;

public static class LatheAmmoCategories
{
    public const string CaliberPrefix = "AmmoCaliber";

    public static readonly ProtoId<LatheCategoryPrototype> KindBox = "AmmoKindBox";
    public static readonly ProtoId<LatheCategoryPrototype> KindMagazine = "AmmoKindMagazine";
    public static readonly ProtoId<LatheCategoryPrototype> KindSpeedLoader = "AmmoKindSpeedLoader";
    public static readonly ProtoId<LatheCategoryPrototype> KindMisc = "AmmoKindMisc";

    public static readonly ProtoId<LatheCategoryPrototype>[] KindFilters =
    [
        KindBox,
        KindMagazine,
        KindSpeedLoader,
    ];

    public static bool IsCaliber(ProtoId<LatheCategoryPrototype> category)
    {
        return category.Id.StartsWith(CaliberPrefix, StringComparison.Ordinal);
    }

    public static bool HasCaliber(LatheRecipePrototype recipe)
    {
        foreach (var category in recipe.Categories)
        {
            if (IsCaliber(category))
                return true;
        }

        return false;
    }

    public static bool MatchesKind(LatheRecipePrototype recipe, ProtoId<LatheCategoryPrototype> kind)
    {
        foreach (var category in recipe.Categories)
        {
            if (category == kind)
                return true;
        }

        return false;
    }
}
