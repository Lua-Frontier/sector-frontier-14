using Content.Shared.Buckle.Components;
using Content.Shared._Goobstation.Vehicles; // Frontier: migrate under _Goobstation
using Content.Shared._Mono.Radar; // Frontier

namespace Content.Server._Goobstation.Vehicles; // Frontier: migrate under _Goobstation

public sealed class VehicleSystem : SharedVehicleSystem
{
    /// <summary>
    /// Configures the radar blip for a vehicle entity.
    /// </summary>
    protected override void OnStrapped(Entity<VehicleComponent> ent, ref StrappedEvent args)
    {
        base.OnStrapped(ent, ref args);
        EnsureComp<RadarBlipComponent>(ent);
    }

    protected override void OnUnstrapped(Entity<VehicleComponent> ent, ref UnstrappedEvent args)
    {
        if (ent.Comp.Driver == args.Buckle.Owner)
        { RemComp<RadarBlipComponent>(ent); }
        base.OnUnstrapped(ent, ref args);
    }

    protected override void HandleEmag(Entity<VehicleComponent> ent)
    {
        RemComp<RadarBlipComponent>(ent);
    }

    protected override void HandleUnemag(Entity<VehicleComponent> ent)
    {
        if (ent.Comp.Driver != null)
            EnsureComp<RadarBlipComponent>(ent);
    }
}
