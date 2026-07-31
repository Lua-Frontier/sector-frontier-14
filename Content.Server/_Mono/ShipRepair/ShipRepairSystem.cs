using Content.Shared._Mono.ShipRepair;
using Content.Shared._Mono.Shipyard;

namespace Content.Server._Mono.ShipRepair;

public sealed partial class ShipRepairSystem : SharedShipRepairSystem
{
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        // SF raises ShipyardShuttlePurchaseEvent as a broadcast event (not directed at the shuttle).
        SubscribeLocalEvent<ShipyardShuttlePurchaseEvent>(OnShipBought);
        SubscribeLocalEvent<InitRepairSnapshotComponent, MapInitEvent>(OnInitSnapshot);

        InitCommands();
        InitGhosts();
    }

    private void OnShipBought(ShipyardShuttlePurchaseEvent ev)
    {
        GenerateRepairData(ev.Shuttle);
    }

    private void OnInitSnapshot(Entity<InitRepairSnapshotComponent> ent, ref MapInitEvent ev)
    {
        GenerateRepairData(ent);
    }
}
