using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Shared.Chemistry.EntitySystems;

public sealed class SolutionRegenerationSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SolutionRegenerationComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SolutionRegenerationComponent, EntRemovedFromContainerMessage>(OnEntRemoved);
    }

    private void OnMapInit(Entity<SolutionRegenerationComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextRegenTime = _timing.CurTime + ent.Comp.Duration;

        Dirty(ent);
    }

    // Workaround for https://github.com/space-wizards/space-station-14/pull/35314
    private void OnEntRemoved(Entity<SolutionRegenerationComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        // Make sure the removed entity was our contained solution and clear our cached reference
        if (args.Entity == ent.Comp.SolutionRef?.Owner)
            ent.Comp.SolutionRef = null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<SolutionRegenerationComponent, SolutionContainerManagerComponent>();
        while (query.MoveNext(out var uid, out var regen, out var manager))
        {
            if (curTime < regen.NextRegenTime)
                continue;

            regen.NextRegenTime += regen.Duration;
            Dirty(uid, regen);

            if (!_solutionContainer.ResolveSolution((uid, manager),
                    regen.SolutionName,
                    ref regen.SolutionRef,
                    out var solution))
                continue;

            var amount = FixedPoint2.Min(solution.AvailableVolume, regen.Generated.Volume);
            if (amount <= FixedPoint2.Zero)
                continue;

            var generated = amount == regen.Generated.Volume
                ? regen.Generated
                : ScaleProportional(regen.Generated, amount);

            _solutionContainer.TryAddSolution(regen.SolutionRef.Value, generated);
        }
    }
    private static Solution ScaleProportional(Solution source, FixedPoint2 amount)
    {
        var result = new Solution(source.Contents.Count) { Temperature = source.Temperature };
        var effVol = source.Volume.Value;
        var remaining = (long) amount.Value;

        for (var i = source.Contents.Count - 1; i >= 0; i--)
        {
            var (reagent, quantity) = source.Contents[i];
            var split = remaining * quantity.Value / effVol;

            if (split <= 0)
            {
                effVol -= quantity.Value;
                continue;
            }

            result.AddReagent(reagent, FixedPoint2.FromCents((int) split));
            remaining -= split;
            effVol -= quantity.Value;
        }

        return result;
    }
}
