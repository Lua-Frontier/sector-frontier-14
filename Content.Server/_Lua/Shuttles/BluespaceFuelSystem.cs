using Content.Shared._Mono.Ships;
using Robust.Server.Containers;
using Robust.Shared.Containers;
using Content.Shared.Stacks;
using Content.Shared.Shuttles.Components;

namespace Content.Server._Mono.Ships;

public sealed class BluespaceFuelSystem : EntitySystem
{
    [Dependency] private readonly ContainerSystem _container = default!;
    private readonly HashSet<EntityUid> _consumedAtStart = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BluespaceFuelComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BluespaceFuelComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<BluespaceFuelComponent, EntRemovedFromContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<FTLComponent, ComponentStartup>(OnFtlStartup);
    }

    private void OnStartup(EntityUid uid, BluespaceFuelComponent comp, ComponentStartup args)
    { UpdateFuelState(uid, comp); }

    private void OnContainerChanged(EntityUid uid, BluespaceFuelComponent comp, EntityEventArgs args)
    { UpdateFuelState(uid, comp); }

    private void UpdateFuelState(EntityUid uid, BluespaceFuelComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false)) return;
        comp.HasFuel = TryGetFuelContainer(uid, out var container) && container != null && container.Count > 0;
        comp.Count = 0;
        comp.MaxCount = 0;
        if (container != null)
        {
            var sharedStack = Get<SharedStackSystem>();
            foreach (var ent in container.ContainedEntities)
            {
                if (TryComp(ent, out StackComponent? stack))
                {
                    comp.Count += stack.Count;
                    comp.MaxCount += sharedStack.GetMaxCount(stack); // test
                }
                else
                {
                    comp.Count += 1;
                    comp.MaxCount += 1;
                }
            }
        }
        Dirty(uid, comp);
    }

    private bool TryGetFuelContainer(EntityUid uid, out BaseContainer? container)
    {
        container = null;
        if (!_container.TryGetContainer(uid, "fuelSlot", out var cont)) return false;
        container = cont;
        return true;
    }

    private void OnFtlStartup(EntityUid uid, FTLComponent comp, ComponentStartup args)
    {
        if (_consumedAtStart.Contains(uid)) return;

        foreach (var (fuelComp, xform) in EntityQuery<BluespaceFuelComponent, TransformComponent>())
        {
            if (xform.GridUid != uid) continue;
            if (!TryGetFuelContainer(fuelComp.Owner, out var container) || container == null || container.Count <= 0) continue;
            var ent = container.ContainedEntities.Count > 0 ? container.ContainedEntities[0] : default;
            if (ent == default) continue;
            if (TryComp(ent, out StackComponent? stack))
            {
                var sharedStack = Get<SharedStackSystem>();
                sharedStack.Use(ent, 1, stack);
            }
            else
            { QueueDel(ent); }
            UpdateFuelState(fuelComp.Owner, fuelComp);
            _consumedAtStart.Add(uid);  break;
        }
    }

    private void ResetConsumed(EntityUid uid)
    { _consumedAtStart.Remove(uid); }
}


