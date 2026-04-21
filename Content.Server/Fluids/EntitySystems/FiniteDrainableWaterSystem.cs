using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Fluids.Components;

namespace Content.Server.Fluids.EntitySystems;

public sealed class FiniteDrainableWaterSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DrainableSolutionComponent, SolutionContainerChangedEvent>(OnSolutionChanged);
    }

    private void OnSolutionChanged(Entity<DrainableSolutionComponent> entity, ref SolutionContainerChangedEvent args)
    {
        if (!TryComp<DeleteWhenDrainedComponent>(entity, out var deleteWhen))
            return;

        if (args.SolutionId != entity.Comp.Solution)
            return;

        if (args.Solution.Volume < deleteWhen.Threshold)
            QueueDel(entity);
    }
}
