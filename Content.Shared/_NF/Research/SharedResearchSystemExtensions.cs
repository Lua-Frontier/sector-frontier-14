using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Research.Systems;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Shared._NF.Research;

public static class SharedResearchSystemExtensions
{
    public static int GetTierCompletionPercentage(this SharedResearchSystem system,
        EntityUid uid,
        TechnologyDatabaseComponent component,
        TechDisciplinePrototype techDiscipline,
        IPrototypeManager prototypeManager)
    {
        if (!system.IsDisciplineFactionAllowed(uid, techDiscipline))
            return 0;

        var allTech = prototypeManager.EnumeratePrototypes<TechnologyPrototype>()
            .Where(p => p.HasDiscipline(techDiscipline.ID) && !p.Hidden && system.IsTechnologyFactionAllowed(uid, p)).ToList();

        if (allTech.Count == 0)
            return 0;

        var percentage = (float)component.UnlockedTechnologies
            .Where(x =>
            {
                var tech = prototypeManager.Index<TechnologyPrototype>(x);
                return tech.HasDiscipline(techDiscipline.ID) && system.IsTechnologyFactionAllowed(uid, tech);
            })
            .Count() / (float)allTech.Count * 100f;

        return (int)Math.Clamp(percentage, 0, 100);
    }
}
