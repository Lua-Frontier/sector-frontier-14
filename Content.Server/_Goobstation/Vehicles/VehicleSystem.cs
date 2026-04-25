using Content.Shared.Buckle.Components;
using Content.Shared._Goobstation.Vehicles; // Frontier: migrate under _Goobstation

namespace Content.Server._Goobstation.Vehicles; // Frontier: migrate under _Goobstation

public sealed class VehicleSystem : SharedVehicleSystem
{
    protected override void OnStrapped(Entity<VehicleComponent> ent, ref StrappedEvent args)
    {
        base.OnStrapped(ent, ref args);
    }

    protected override void OnUnstrapped(Entity<VehicleComponent> ent, ref UnstrappedEvent args)
    {
        base.OnUnstrapped(ent, ref args);
    }

    protected override void HandleEmag(Entity<VehicleComponent> ent) { }

    protected override void HandleUnemag(Entity<VehicleComponent> ent) { }
}
