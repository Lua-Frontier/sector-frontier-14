using Content.Shared.Morgue.Components;
using Content.Shared.Storage.Components;

namespace Content.Shared.Morgue;

public abstract class SharedCrematoriumSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActiveCrematoriumComponent, StorageOpenAttemptEvent>(OnAttemptOpen);
    }

    private void OnAttemptOpen(Entity<ActiveCrematoriumComponent> ent, ref StorageOpenAttemptEvent args)
    {
        args.Cancelled = true;
    }
}
