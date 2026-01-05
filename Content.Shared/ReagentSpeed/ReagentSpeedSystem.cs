using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Components; // Lua

namespace Content.Shared.ReagentSpeed;

public sealed class ReagentSpeedSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;

    /// <summary>
    /// Consumes reagents and modifies the duration.
    /// This can be production time firing delay etc.
    /// </summary>
    public TimeSpan ApplySpeed(Entity<ReagentSpeedComponent?> ent, TimeSpan time)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return time;

        if (!_solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out _, out var solution))
            return time;

        time *= GetTimeModifier(ent.Comp, solution); // Lua

        foreach (var (reagent, fullModifier) in ent.Comp.Modifiers)
        {
            // Lua start

            solution.RemoveReagent(reagent, ent.Comp.Cost);
            //var used = solution.RemoveReagent(reagent, ent.Comp.Cost);
            //var efficiency = (used / ent.Comp.Cost).Float();
            // scale the speed modifier so microdosing has less effect
            //var reduction = (1f - fullModifier) * efficiency;
            //var modifier = 1f - reduction;
            //time *= modifier;

            // Lua end
        }

        return time;
    }

    // Lua start

    public float GetTimeModifier(Entity<ReagentSpeedComponent?> ent)
    {
        var modifier = 1f;

        if (!Resolve(ent, ref ent.Comp, false))
            return modifier;

        if (!_solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out _, out var solution))
            return modifier;

        return GetTimeModifier(ent.Comp, solution);
    }

    public static float GetTimeModifier(ReagentSpeedComponent comp, Solution solution)
    {
        var modifier = 1f;

        foreach (var (reagent, fullModifier) in comp.Modifiers)
        {
            var efficiency = Math.Min((solution.GetTotalPrototypeQuantity(reagent) / comp.Cost).Float(), 1f);
            var reduction = (1f - fullModifier) * efficiency;
            var reagentModifier = 1f - reduction;
            modifier *= reagentModifier;
        }

        return modifier;
    }
    // Lua end
}
