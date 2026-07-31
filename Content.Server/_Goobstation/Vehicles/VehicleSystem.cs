using Content.Shared._Mono.Radar;
using Content.Shared.Buckle.Components;
using Content.Shared._Goobstation.Vehicles; // Frontier: migrate under _Goobstation

namespace Content.Server._Goobstation.Vehicles; // Frontier: migrate under _Goobstation

public sealed class VehicleSystem : SharedVehicleSystem
{
    protected override void OnStrapped(Entity<VehicleComponent> ent, ref StrappedEvent args)
    {
        base.OnStrapped(ent, ref args);

        // Mono: show occupied vehicles on mass scanner
        var blip = EnsureComp<RadarBlipComponent>(ent);
        blip.RadarColor = Color.Cyan;
        blip.Scale = 0.5f;
        blip.Shape = RadarBlipShape.Circle;
        blip.VisibleFromOtherGrids = true;
        blip.GridConfig = new BlipConfig
        {
            Color = Color.Cyan,
            Shape = RadarBlipShape.Square,
            RespectZoom = true,
            Rotate = true,
            Bounds = new Box2(-0.25f, -0.25f, 0.25f, 0.25f),
        };
    }

    protected override void OnUnstrapped(Entity<VehicleComponent> ent, ref UnstrappedEvent args)
    {
        RemComp<RadarBlipComponent>(ent);

        base.OnUnstrapped(ent, ref args);
    }

    protected override void HandleEmag(Entity<VehicleComponent> ent) { }

    protected override void HandleUnemag(Entity<VehicleComponent> ent) { }
}
