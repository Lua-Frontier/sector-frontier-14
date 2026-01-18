using Content.Shared._Goobstation.Vehicles;
using Content.Shared._NF.Vehicle.Components;
using Content.Shared.Buckle.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Graphics.RSI;
using System.Numerics;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._NF.Vehicles;

// Rewritten from Goobstation's VehicleSystem.
public sealed class VehicleSystem : SharedVehicleSystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VehicleComponent, AppearanceChangeEvent>(OnAppearanceChange);
        SubscribeLocalEvent<VehicleRiderComponent, ComponentStartup>(OnRiderStartup);
    }

    private void OnAppearanceChange(EntityUid uid, VehicleComponent comp, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null
            || !_appearance.TryGetData(uid, VehicleState.Animated, out bool animated)
            || !TryComp<SpriteComponent>(uid, out var spriteComp))
        {
            return;
        }

        if (!spriteComp.LayerMapTryGet(VehicleVisualLayers.AutoAnimate, out var layer))
            layer = 0;
        spriteComp.LayerSetAutoAnimated(layer, animated);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<VehicleComponent, SpriteComponent>();
        var eye = _eye.CurrentEye;
        while (query.MoveNext(out var uid, out var vehicle, out var sprite))
        {
            var angle = _transform.GetWorldRotation(uid) + eye.Rotation;
            if (angle < 0)
                angle += 2 * Math.PI;
            RsiDirection dir = SpriteComponent.Layer.GetDirection(RsiDirectionType.Dir4, angle);
            VehicleRenderOver renderOver = (VehicleRenderOver)(1 << (int)dir);

            if ((vehicle.RenderOver & renderOver) == renderOver)
                sprite.DrawDepth = (int)Content.Shared.DrawDepth.DrawDepth.OverMobs;
            else
                sprite.DrawDepth = (int)Content.Shared.DrawDepth.DrawDepth.Objects;

            Vector2 offset = Vector2.Zero;
            if (vehicle.Driver != null)
            {
                switch (dir)
                {
                    case RsiDirection.South:
                    default:
                        offset = vehicle.SouthOffset;
                        break;
                    case RsiDirection.North:
                        offset = vehicle.NorthOffset;
                        break;
                    case RsiDirection.East:
                        offset = vehicle.EastOffset;
                        break;
                    case RsiDirection.West:
                        offset = vehicle.WestOffset;
                        break;
                }
            }

            // Avoid recalculating a matrix if we can help it.
            if (sprite.Offset != offset)
                sprite.Offset = offset;
        }
        var riderQuery = EntityQueryEnumerator<VehicleRiderComponent, SpriteComponent, BuckleComponent>();
        while (riderQuery.MoveNext(out var riderUid, out var riderComp, out var riderSprite, out var buckle))
        {
            if (buckle.BuckledTo is not { } vehicleUid) continue;
            if (!TryComp<VehicleComponent>(vehicleUid, out var vehicle)) continue;
            if (vehicle.Passenger != riderUid) continue;
            var vehicleAngle = _transform.GetWorldRotation(vehicleUid) + eye.Rotation;
            if (vehicleAngle < 0) vehicleAngle += 2 * Math.PI;
            RsiDirection vehicleDir = SpriteComponent.Layer.GetDirection(RsiDirectionType.Dir4, vehicleAngle);
            if (vehicleDir == RsiDirection.North)
            {
                if (riderSprite.DrawDepth > (int)DrawDepth.Mobs)
                {
                    riderComp.OriginalDrawDepth ??= riderSprite.DrawDepth;
                    _sprite.SetDrawDepth((riderUid, riderSprite), (int)DrawDepth.Mobs);
                }
            }
            else if (riderComp.OriginalDrawDepth.HasValue)
            {
                _sprite.SetDrawDepth((riderUid, riderSprite), riderComp.OriginalDrawDepth.Value);
                riderComp.OriginalDrawDepth = null;
            }
        }
    }

    private void OnRiderStartup(Entity<VehicleRiderComponent> ent, ref ComponentStartup args)
    { }

    // NOOPs
    protected override void HandleEmag(Entity<VehicleComponent> ent)
    {
    }

    protected override void HandleUnemag(Entity<VehicleComponent> ent)
    {
    }
}

public enum VehicleVisualLayers : byte
{
    /// Layer for the vehicle's wheels/jets/etc.
    AutoAnimate,
}
