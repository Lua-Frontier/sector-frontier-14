// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server._Lua.Shuttles.Components;
using Content.Server.Shuttles.Components;
using Content.Shared._Lua.Shuttles;
using Content.Shared._Lua.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleSystem
{
    private const string MagneticLatchJointPrefix = "magnetic-latch-";
    private const int LatchSearchRadiusTiles = 1;

    partial void HandleShuttleCollision(ref bool handled, EntityUid uid, ShuttleComponent component, ref StartCollideEvent args, MapGridComponent ourGrid, MapGridComponent otherGrid)
    {
        if (handled) return;
        if (args.OurEntity != uid) return;
        var ourXform = Transform(args.OurEntity);
        var otherXform = Transform(args.OtherEntity);
        if (args.WorldPoints.Length == 0) return;
        var ourIntersecting = new HashSet<EntityUid>();
        var otherIntersecting = new HashSet<EntityUid>();
        void CollectNeighborhood(EntityUid gridUid, MapGridComponent grid, Vector2i centerTile, HashSet<EntityUid> output)
        { for (var dx = -LatchSearchRadiusTiles; dx <= LatchSearchRadiusTiles; dx++) { for (var dy = -LatchSearchRadiusTiles; dy <= LatchSearchRadiusTiles; dy++) { _lookup.GetLocalEntitiesIntersecting(gridUid, centerTile + new Vector2i(dx, dy), output, gridComp: grid); } } }
        foreach (var worldPoint in args.WorldPoints)
        {
            var otherPoint = _transform.ToCoordinates((args.OtherEntity, otherXform), new MapCoordinates(worldPoint, otherXform.MapID));
            var otherTile = new Vector2i((int)Math.Floor(otherPoint.X / otherGrid.TileSize), (int)Math.Floor(otherPoint.Y / otherGrid.TileSize));
            otherIntersecting.Clear();
            CollectNeighborhood(args.OtherEntity, otherGrid, otherTile, otherIntersecting);
            var hasWall = false;
            EntityUid? wallUid = null;
            foreach (var ent in otherIntersecting)
            {
                if (_tags.HasTag(ent, "Wall"))
                {
                    hasWall = true;
                    wallUid = ent;
                    break;
                }
            }
            if (!hasWall || wallUid == null) continue;
            var ourPoint = _transform.ToCoordinates((args.OurEntity, ourXform), new MapCoordinates(worldPoint, ourXform.MapID));
            var ourTile = new Vector2i((int)Math.Floor(ourPoint.X / ourGrid.TileSize), (int)Math.Floor(ourPoint.Y / ourGrid.TileSize));
            ourIntersecting.Clear();
            CollectNeighborhood(args.OurEntity, ourGrid, ourTile, ourIntersecting);
            EntityUid? magnetUid = null;
            TransformComponent? magnetXform = null;
            float blyaDistSq = float.MaxValue;
            foreach (var ent in ourIntersecting)
            {
                if (!TryComp(ent, out MagneticGrabberComponent? grabber) || !grabber.Enabled) continue;
                var xform = Transform(ent);
                if (!xform.Anchored || xform.GridUid != uid) continue;
                if (TryComp(ent, out MagneticLatchComponent? latched) && latched.JointId != null) continue;
                if (TryComp(ent, out MagneticGrabberCooldownComponent? cooldown) && _gameTiming.CurTime < cooldown.NextLatchAllowed)
                { continue; }
                var diff = xform.LocalPosition - ourPoint.Position;
                var distSq = diff.LengthSquared();
                if (distSq >= blyaDistSq) continue;
                blyaDistSq = distSq;
                magnetUid = ent;
                magnetXform = xform;
            }
            if (magnetUid == null || magnetXform == null) continue;
            var jointId = MagneticLatchJointPrefix + magnetUid.Value;
            if (!_physicsQuery.TryGetComponent(args.OurEntity, out var ourPhys) || !_physicsQuery.TryGetComponent(args.OtherEntity, out var otherPhys)) { return; }
            SharedJointSystem.LinearStiffness(8f, 1.0f, ourPhys.Mass, otherPhys.Mass, out var stiffness, out var damping);
            var joint = _joints.GetOrCreateWeldJoint(args.OurEntity, args.OtherEntity, jointId);
            joint.LocalAnchorA = ourPoint.Position;
            joint.LocalAnchorB = otherPoint.Position;
            joint.ReferenceAngle = (float)(_transform.GetWorldRotation(otherXform) - _transform.GetWorldRotation(ourXform));
            joint.CollideConnected = false;
            joint.Stiffness = stiffness;
            joint.Damping = damping;
            var linear = (ourPhys.LinearVelocity + otherPhys.LinearVelocity) / 2f;
            _physics.SetLinearVelocity(args.OurEntity, linear, body: ourPhys);
            _physics.SetLinearVelocity(args.OtherEntity, linear, body: otherPhys);
            _physics.SetAngularVelocity(args.OurEntity, 0f, body: ourPhys);
            _physics.SetAngularVelocity(args.OtherEntity, 0f, body: otherPhys);
            var latch = EnsureComp<MagneticLatchComponent>(magnetUid.Value);
            latch.JointId = jointId;
            latch.OwnerGrid = args.OurEntity;
            latch.TargetGrid = args.OtherEntity;
            latch.LatchedToEntity = wallUid;
            latch.LocalAnchorOwner = ourPoint.Position;
            latch.LocalAnchorTarget = otherPoint.Position;
            latch.ReferenceAngle = joint.ReferenceAngle;
            Dirty(magnetUid.Value, latch);
            if (TryComp(magnetUid.Value, out DockingComponent? dock))
            {
                dock.DockedWith = wallUid;
                Dirty(magnetUid.Value, dock);
            }
            _appearance.SetData(magnetUid.Value, MagneticLatchVisuals.State, MagneticLatchVisualState.Latched);
            handled = true;
            return;
        }
    }
}

