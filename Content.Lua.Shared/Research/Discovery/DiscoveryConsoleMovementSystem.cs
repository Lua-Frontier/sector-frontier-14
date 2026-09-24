using Content.Shared.ActionBlocker;
using Content.Shared.Movement.Events;

namespace Content.Lua.Shared.Research.Discovery;

public abstract class DiscoveryConsoleMovementSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DiscoveryConsoleOperatorComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
        SubscribeLocalEvent<DiscoveryConsoleOperatorComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<DiscoveryConsoleOperatorComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(EntityUid uid, DiscoveryConsoleOperatorComponent component, ComponentStartup args)
    {
        _actionBlocker.UpdateCanMove(uid);
    }

    private void OnShutdown(EntityUid uid, DiscoveryConsoleOperatorComponent component, ComponentShutdown args)
    {
        _actionBlocker.UpdateCanMove(uid);
        OnOperatorShutdown(uid);
    }

    protected virtual void OnOperatorShutdown(EntityUid uid)
    {
    }

    private void OnUpdateCanMove(EntityUid uid, DiscoveryConsoleOperatorComponent component, UpdateCanMoveEvent args)
    {
        if (component.LifeStage > ComponentLifeStage.Running)
            return;

        args.Cancel();
    }
}
