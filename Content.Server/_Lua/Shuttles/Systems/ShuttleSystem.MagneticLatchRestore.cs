// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Shared._Lua.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleSystem
{
    partial void HandleMagneticLatchFtlCompleted(Entity<ShuttleComponent> ent, ref FTLCompletedEvent args)
    {
        var gridUid = ent.Owner;
        Timer.Spawn(TimeSpan.FromMilliseconds(250), () => TryRestoreMagneticLatches(gridUid, attempt: 0));
    }

    private void TryRestoreMagneticLatches(EntityUid gridUid, int attempt)
    {
        if (TerminatingOrDeleted(gridUid)) return;
        var gridXform = Transform(gridUid);
        if (gridXform.MapID == MapId.Nullspace)
        {
            Reschedule(gridUid, attempt);
            return;
        }
        var latchSet = new HashSet<Entity<MagneticLatchComponent, TransformComponent>>();
        _lookup.GetChildEntities(gridUid, latchSet);
        foreach (var magnet in latchSet)
        {
            var latch = magnet.Comp1;
            var xform = magnet.Comp2;
            if (latch.JointId == null || latch.OwnerGrid != gridUid || latch.TargetGrid == null || latch.LocalAnchorOwner == null || latch.LocalAnchorTarget == null) { continue; }
            if (xform.GridUid != gridUid) continue;
            var targetGrid = latch.TargetGrid.Value;
            if (TerminatingOrDeleted(targetGrid) || !HasComp<ShuttleComponent>(targetGrid)) continue;
            var targetXform = Transform(targetGrid);
            if (targetXform.MapID == MapId.Nullspace || targetXform.MapID != gridXform.MapID)
            {
                Reschedule(gridUid, attempt);
                continue;
            }
            if (!_physicsQuery.TryGetComponent(gridUid, out var ourPhys) || !_physicsQuery.TryGetComponent(targetGrid, out var otherPhys))
            {
                Reschedule(gridUid, attempt);
                continue;
            }
            SharedJointSystem.LinearStiffness(MagneticLatchFrequencyHz, MagneticLatchDampingRatio, ourPhys.Mass, otherPhys.Mass, out var stiffness, out var damping);
            PreAlignLatch(gridUid, targetGrid, latch);
            var joint = _joints.GetOrCreateWeldJoint(gridUid, targetGrid, latch.JointId);
            joint.LocalAnchorA = latch.LocalAnchorOwner.Value;
            joint.LocalAnchorB = latch.LocalAnchorTarget.Value;
            joint.ReferenceAngle = latch.ReferenceAngle ?? joint.ReferenceAngle;
            joint.CollideConnected = false;
            joint.Stiffness = stiffness;
            joint.Damping = damping;
            if (TryComp(magnet.Owner, out DockingComponent? dock) && dock.DockedWith == null && latch.LatchedToEntity != null)
            {
                dock.DockedWith = latch.LatchedToEntity;

            }
        }
    }

    private void Reschedule(EntityUid gridUid, int attempt)
    {
        if (attempt >= 10) return;
        Timer.Spawn(TimeSpan.FromMilliseconds(250), () => TryRestoreMagneticLatches(gridUid, attempt + 1));
    }

    private void PreAlignLatch(EntityUid ownerGrid, EntityUid targetGrid, MagneticLatchComponent latch)
    {
        if (latch.LocalAnchorOwner == null || latch.LocalAnchorTarget == null) return;
        var ownerXform = Transform(ownerGrid);
        var targetXform = Transform(targetGrid);
        if (ownerXform.MapID == MapId.Nullspace || ownerXform.MapID != targetXform.MapID) return;
        var ownerRot = _transform.GetWorldRotation(ownerXform);
        var ownerPos = _transform.GetWorldPosition(ownerXform);
        var desiredTargetRot = ownerRot + new Angle(latch.ReferenceAngle ?? 0f);
        var worldAnchorOwner = ownerPos + ownerRot.RotateVec(latch.LocalAnchorOwner.Value);
        var desiredTargetPos = worldAnchorOwner - desiredTargetRot.RotateVec(latch.LocalAnchorTarget.Value);
        _transform.SetWorldRotationNoLerp(targetGrid, desiredTargetRot);
        _transform.SetWorldPosition(targetGrid, desiredTargetPos);
    }
}

