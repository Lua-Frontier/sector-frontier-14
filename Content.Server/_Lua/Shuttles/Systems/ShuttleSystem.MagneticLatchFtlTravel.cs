// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server.Shuttles.Components;
using Content.Shared._Lua.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleSystem
{
    private void RestoreMagneticLatchesInHyperspace(HashSet<EntityUid> dockedShuttles)
    {
        var latchSet = new HashSet<Entity<MagneticLatchComponent, TransformComponent>>();
        foreach (var gridUid in dockedShuttles)
        {
            if (TerminatingOrDeleted(gridUid) || !HasComp<ShuttleComponent>(gridUid)) continue;
            var gridXform = Transform(gridUid);
            if (gridXform.MapID == MapId.Nullspace) continue;
            latchSet.Clear();
            _lookup.GetChildEntities(gridUid, latchSet);
            foreach (var magnet in latchSet)
            {
                var latch = magnet.Comp1;
                var magnetXform = magnet.Comp2;
                if (latch.JointId == null || latch.OwnerGrid != gridUid || latch.TargetGrid == null || latch.LocalAnchorOwner == null || latch.LocalAnchorTarget == null) { continue; }
                if (magnetXform.GridUid != gridUid) continue;
                var targetGrid = latch.TargetGrid.Value;
                if (!dockedShuttles.Contains(targetGrid)) continue;
                if (TerminatingOrDeleted(targetGrid) || !HasComp<ShuttleComponent>(targetGrid)) continue;
                var targetXform = Transform(targetGrid);
                if (targetXform.MapID == MapId.Nullspace || targetXform.MapID != gridXform.MapID) continue;
                if (!_physicsQuery.TryGetComponent(gridUid, out var ourPhys) || !_physicsQuery.TryGetComponent(targetGrid, out var otherPhys))
                { continue; }
                SharedJointSystem.LinearStiffness(MagneticLatchFrequencyHz, MagneticLatchDampingRatio, ourPhys.Mass, otherPhys.Mass, out var stiffness, out var damping);
                var ownerRot = _transform.GetWorldRotation(gridXform);
                var ownerPos = _transform.GetWorldPosition(gridXform);
                var desiredTargetRot = ownerRot + new Angle(latch.ReferenceAngle ?? 0f);
                var worldAnchorOwner = ownerPos + ownerRot.RotateVec(latch.LocalAnchorOwner.Value);
                var desiredTargetPos = worldAnchorOwner - desiredTargetRot.RotateVec(latch.LocalAnchorTarget.Value);
                _transform.SetWorldRotationNoLerp(targetGrid, desiredTargetRot);
                _transform.SetWorldPosition(targetGrid, desiredTargetPos);
                var joint = _joints.GetOrCreateWeldJoint(gridUid, targetGrid, latch.JointId);
                joint.LocalAnchorA = latch.LocalAnchorOwner.Value;
                joint.LocalAnchorB = latch.LocalAnchorTarget.Value;
                joint.ReferenceAngle = latch.ReferenceAngle ?? joint.ReferenceAngle;
                joint.CollideConnected = false;
                joint.Stiffness = stiffness;
                joint.Damping = damping;
            }
        }
    }
}

