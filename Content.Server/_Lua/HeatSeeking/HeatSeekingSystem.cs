//REV
// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using System.Linq;
using System.Numerics;
using Content.Shared.Interaction;
using Content.Shared.Physics;
using Robust.Server.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Random;

namespace Content.Server._Lua.HeatSeeking;

/// <summary>
/// This handles...
/// </summary>
public sealed class HeatSeekingSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly RotateToFaceSystem _rotate = default!;
    [Dependency] private readonly PhysicsSystem _physics = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HeatSeekingComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.TargetEntity.HasValue)
            {
                var entXform = Transform(comp.TargetEntity.Value);
                var angle = (
                    _transform.ToMapCoordinates(xform.Coordinates).Position -
                    _transform.ToMapCoordinates(entXform.Coordinates).Position
                ).ToWorldAngle();

                _transform.SetLocalRotationNoLerp(uid, angle, xform);

                if (!_rotate.TryRotateTo(uid, angle, frameTime, comp.WeaponArc, comp.RotationSpeed?.Theta ?? double.MaxValue, xform))
                {
                    continue;
                }

                _physics.ApplyForce(uid, xform.LocalRotation.RotateVec(new Vector2(0, 1)) * comp.Acceleration);
                return;
            }

            var ray = new CollisionRay(_transform.GetMapCoordinates(uid, xform).Position,
                xform.LocalRotation.ToWorldVec(),
                (int) (CollisionGroup.Impassable | CollisionGroup.BulletImpassable));
            var results = _physics.IntersectRay(xform.MapID, ray, comp.DefaultSeekingRange, uid).ToList();
            if (results.Count <= 0)
                return; // nothing to heatseek ykwim

            if (comp is { LockedIn: true, TargetEntity: not null })
                return; // Don't reassign target entity if we have one AND we have the LockedIn property

            comp.TargetEntity = results[0].HitEntity;
        }
    }
}
