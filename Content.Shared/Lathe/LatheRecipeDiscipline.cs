using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Lathe;

public static class LatheRecipeDiscipline
{
    public const string Basic = "Basic";
    public static Dictionary<string, string> BuildMap(IPrototypeManager prototypes)
    {
        var map = new Dictionary<string, string>();

        foreach (var tech in prototypes.EnumeratePrototypes<TechnologyPrototype>())
        {
            foreach (var recipe in tech.RecipeUnlocks)
            {
                map.TryAdd(recipe, tech.Discipline);
            }
        }

        foreach (var recipe in prototypes.EnumeratePrototypes<LatheRecipePrototype>())
        {
            if (recipe.ResearchDiscipline is { } explicitDisc)
                map[recipe.ID] = explicitDisc;
            else if (!map.ContainsKey(recipe.ID))
                map[recipe.ID] = Basic;
        }

        return map;
    }

    public static string GetDiscipline(Dictionary<string, string> map, string recipeId)
    {
        return map.TryGetValue(recipeId, out var disc) ? disc : Basic;
    }
}
