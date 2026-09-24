using Content.Shared.Research.Prototypes;
using Content.Shared._Mono.Company;
using Robust.Shared.Prototypes;

namespace Content.Client.Lua.Research.Discovery;

internal static class DiscoveryUiLoc
{
    public static string DisciplineName(string? disciplineId, IPrototypeManager prototypes)
    {
        if (string.IsNullOrEmpty(disciplineId))
            return "?";

        if (prototypes.TryIndex<TechDisciplinePrototype>(disciplineId, out var disc))
            return Loc.GetString(disc.Name);

        return disciplineId;
    }

    public static string FactionName(string? factionId, IPrototypeManager prototypes)
    {
        if (string.IsNullOrEmpty(factionId))
            return "-";

        if (prototypes.TryIndex<RndFactionPrototype>(factionId, out var rnd) &&
            !string.IsNullOrWhiteSpace(rnd.Name))
            return rnd.Name;

        var locKey = $"entity-category-name-{factionId.ToLowerInvariant()}";
        if (Loc.TryGetString(locKey, out var localized) && !string.IsNullOrWhiteSpace(localized))
            return localized;

        if (prototypes.TryIndex<CompanyPrototype>(factionId, out var company) &&
            !string.IsNullOrWhiteSpace(company.Name))
            return company.Name;

        return factionId;
    }

    public static string RiskName(string? risk)
    {
        if (string.IsNullOrEmpty(risk))
            return "-";

        var key = $"discovery-research-risk-{risk}";
        return Loc.TryGetString(key, out var localized) ? localized : risk;
    }
}
