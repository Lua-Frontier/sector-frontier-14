// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.
using Content.Server.Shuttles.Components;
using Content.Shared._Lua.AiShuttle;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using System.Linq;
using System.Numerics;

namespace Content.Server._Lua.AiShuttle;

public partial class AiShuttleBrainSystem
{
    private void DriveViaConsole(EntityUid grid, AiShuttleBrainComponent brain, PhysicsComponent body, float controlDt,
        Vector2 dirToTarget, Vector2 tangential, float radialError, bool requestBrake, int sidePrefer, bool gateForwardOnYaw = false)
    {
        if (brain.PilotEntity == null || !TryComp<PilotComponent>(brain.PilotEntity.Value, out var pilot)) return;
        var gridRot = _xform.GetWorldRotation(grid);
        var forwardBase = Angle.FromDegrees(brain.ForwardAngleOffset);
        var nose = (gridRot + forwardBase).RotateVec(Vector2.UnitY);
        var facingVec = dirToTarget;
        if (brain.FacingMarker is { } marker)
        {
            var gridXform = Transform(grid);
            var gridPos = _xform.GetWorldPosition(gridXform);
            var toMarker = marker - gridPos;
            if (toMarker.LengthSquared() > 0.0001f) facingVec = Vector2.Normalize(toMarker);
        }
        var cross = nose.X * facingVec.Y - nose.Y * facingVec.X;
        var dot = Vector2.Dot(nose, facingVec);
        var yawError = MathF.Atan2(cross, dot);
        var desiredButtons = ShuttleButtons.None;
        var yawDeadzone = brain.YawDeadzoneDegrees * (MathF.PI / 180f);
        var kAvPerRad = brain.YawKAvPerRad;
        var avEpsilon = brain.YawAvEpsilon;
        var av = body.AngularVelocity;
        var needTurn = MathF.Abs(yawError) > yawDeadzone;
        var yawGate = brain.YawGateDegrees * (MathF.PI / 180f);
        var allowForward = !gateForwardOnYaw || MathF.Abs(yawError) < yawGate;
        var allowLateral = !gateForwardOnYaw || MathF.Abs(yawError) < yawGate;
        if (needTurn)
        {
            var desiredAv = Math.Clamp(yawError * kAvPerRad, -ShuttleComponent.MaxAngularVelocity, ShuttleComponent.MaxAngularVelocity);
            var avErr = desiredAv - av;
            if (avErr > avEpsilon) desiredButtons |= ShuttleButtons.RotateLeft;
            else if (avErr < -avEpsilon) desiredButtons |= ShuttleButtons.RotateRight;
        }
        else
        {
            var avBrakeThreshold = brain.YawAvBrakeThreshold;
            if (av > avBrakeThreshold) desiredButtons |= ShuttleButtons.RotateRight;
            else if (av < -avBrakeThreshold) desiredButtons |= ShuttleButtons.RotateLeft;
        }
        var desired = tangential * Math.Clamp(brain.OrbitTangentialScale, 0f, 1f) + dirToTarget * Math.Clamp(radialError * 0.5f, -1f, 1f);
        var right = (gridRot + forwardBase).RotateVec(Vector2.UnitX);
        if (sidePrefer != 0)
        {
            Vector2 sideBasis;
            if (dirToTarget.LengthSquared() > 1e-4f) sideBasis = new Vector2(-dirToTarget.Y, dirToTarget.X);
            else sideBasis = right;
            var side = sideBasis * (sidePrefer > 0 ? 1f : -1f);
            desired += side * 0.8f;
        }
        var forward = (gridRot + forwardBase).RotateVec(Vector2.UnitY);
        var fwdAmt = Vector2.Dot(desired, forward);
        var rightAmt = Vector2.Dot(desired, right);
        if (fwdAmt > 0.1f && !requestBrake && allowForward) desiredButtons |= ShuttleButtons.StrafeUp;
        else if (fwdAmt < -0.1f) desiredButtons |= ShuttleButtons.StrafeDown;
        if (rightAmt > 0.1f && allowLateral) desiredButtons |= ShuttleButtons.StrafeRight;
        else if (rightAmt < -0.1f && allowLateral) desiredButtons |= ShuttleButtons.StrafeLeft;
        if (requestBrake)
        {
            var linVel = body.LinearVelocity;
            var fwdVel = Vector2.Dot(linVel, forward);
            if (fwdVel > 0.05f)
                desiredButtons |= ShuttleButtons.StrafeDown;
        }
        if (pilot.HeldButtons != desiredButtons)
            pilot.HeldButtons = desiredButtons;
    }

    private (Vector2 avoid, float forwardHit, float leftClear, float rightClear) ComputeAvoidance(EntityUid grid, Vector2 fromWorld, Vector2 desiredDir, MapId mapId, EntityUid? exclude, bool isPatrolMode = false)
    {
        if (desiredDir == Vector2.Zero) return (Vector2.Zero, 0f, 0f, 0f);
        var dir = Vector2.Normalize(desiredDir);
        float probeDist, sideProbeDist;
        if (isPatrolMode)
        {
            probeDist = 128f;
            sideProbeDist = 128f;
        }
        else
        {
            probeDist = 36f;
            sideProbeDist = 22f;
        }
        var collisionMask = (int)(CollisionGroup.Impassable | CollisionGroup.Opaque | CollisionGroup.MidImpassable | CollisionGroup.HighImpassable | CollisionGroup.LowImpassable | CollisionGroup.BulletImpassable);
        (Vector2 sum, float minDist, float freeDist) AccumulateAvoid(Vector2 start, Vector2 direction, float dist)
        {
            var ray = new Robust.Shared.Physics.CollisionRay(start, direction, collisionMask);
            var hits = _physics.IntersectRay(mapId, ray, dist, grid, returnOnFirstHit: false);
            Vector2 sum = Vector2.Zero;
            float min = float.MaxValue;
            float maxFree = dist;
            foreach (var hit in hits)
            {
                if (TryComp<TransformComponent>(hit.HitEntity, out var selfHx) && selfHx.GridUid == grid) continue;
                if (exclude != null)
                {
                    if (hit.HitEntity == exclude) continue;
                    if (TryComp<TransformComponent>(hit.HitEntity, out var hx) && hx.GridUid == exclude) continue;
                }
                var away = start - hit.HitPos;
                if (away.LengthSquared() > 0.0001f) sum += Vector2.Normalize(away);
                if (hit.Distance > 0f && hit.Distance < min) min = hit.Distance;
                if (hit.Distance > 0f && hit.Distance < maxFree) maxFree = hit.Distance;
            }
            return (sum, hits.Any() ? min : 0f, maxFree);
        }
        var noseStart = fromWorld + dir * 5.5f;
        var leftDir = new Vector2(-dir.Y, dir.X);
        var rightDir = -leftDir;
        var backDir = -dir;
        Vector2 exclusionAvoid = Vector2.Zero;
        var brain = Comp<AiShuttleBrainComponent>(grid);
        if (brain.RespectFtlExclusions)
        {
            var q = EntityQueryEnumerator<FTLExclusionComponent, TransformComponent>();
            while (q.MoveNext(out var excl, out var xform))
            {
                if (!excl.Enabled) continue;
                if (xform.MapID != mapId) continue;
                var center = _xform.GetWorldPosition(xform);
                var toCenter = fromWorld - center;
                var dist = toCenter.Length();
                var range = excl.Range + brain.FtlExclusionBuffer;
                if (dist <= range + 64f)
                { if (dist > 0.001f) exclusionAvoid += Vector2.Normalize(toCenter) * MathF.Max(0f, (range + 64f - dist)) / (range + 64f); }
                var nextPos = fromWorld + dir * (isPatrolMode ? 32f : 8f);
                if ((nextPos - center).Length() <= range)
                {
                    var away = toCenter.LengthSquared() > 1e-4f ? Vector2.Normalize(toCenter) : -dir;
                    exclusionAvoid += away * 3.5f;
                }
            }
        }
        if (isPatrolMode)
        {
            var ahead = AccumulateAvoid(noseStart, dir, probeDist);// 4
            var left = AccumulateAvoid(noseStart, leftDir, sideProbeDist);
            var right = AccumulateAvoid(noseStart, rightDir, sideProbeDist);
            var back = AccumulateAvoid(noseStart, backDir, sideProbeDist);
            var aheadLeft = AccumulateAvoid(noseStart, Vector2.Normalize(dir + leftDir * 0.7f), probeDist * 0.9f);// 8
            var aheadRight = AccumulateAvoid(noseStart, Vector2.Normalize(dir + rightDir * 0.7f), probeDist * 0.9f);
            var backLeft = AccumulateAvoid(noseStart, Vector2.Normalize(backDir + leftDir * 0.7f), sideProbeDist * 0.9f);
            var backRight = AccumulateAvoid(noseStart, Vector2.Normalize(backDir + rightDir * 0.7f), sideProbeDist * 0.9f);
            var leftAhead = AccumulateAvoid(noseStart, Vector2.Normalize(leftDir + dir * 0.7f), sideProbeDist * 0.9f);
            var rightAhead = AccumulateAvoid(noseStart, Vector2.Normalize(rightDir + dir * 0.7f), sideProbeDist * 0.9f);
            var leftBack = AccumulateAvoid(noseStart, Vector2.Normalize(leftDir + backDir * 0.7f), sideProbeDist * 0.9f);
            var rightBack = AccumulateAvoid(noseStart, Vector2.Normalize(rightDir + backDir * 0.7f), sideProbeDist * 0.9f);
            var avoid = ahead.sum * 3.0f + (aheadLeft.sum + aheadRight.sum) * 2.0f + (leftAhead.sum + rightAhead.sum) * 1.5f + left.sum * 1.2f + right.sum * 1.2f + (backLeft.sum + backRight.sum) * 0.8f + (leftBack.sum + rightBack.sum) * 0.6f + back.sum * 0.4f + exclusionAvoid * 2.0f;
            var forwardHits = new[] { ahead.minDist, aheadLeft.minDist, aheadRight.minDist, leftAhead.minDist, rightAhead.minDist };
            var minForwardHit = forwardHits.Where(d => d > 0f).DefaultIfEmpty(float.MaxValue).Min();
            return (avoid, minForwardHit, left.freeDist, right.freeDist);
        }
        else
        {
            var ahead = AccumulateAvoid(noseStart, dir, probeDist);// 4
            var left = AccumulateAvoid(noseStart, leftDir, sideProbeDist);
            var right = AccumulateAvoid(noseStart, rightDir, sideProbeDist);
            var back = AccumulateAvoid(noseStart, backDir, sideProbeDist);
            var avoid = ahead.sum * 2.2f + left.sum * 1.0f + right.sum * 1.0f + back.sum * 0.4f + exclusionAvoid * 2.0f;
            return (avoid, ahead.minDist, left.freeDist, right.freeDist);
        }
    }

    private float ComputeHullDistance(EntityUid selfGrid, Vector2 fromWorld, Vector2 targetWorld, MapId mapId, EntityUid targetGrid)
    {
        var delta = targetWorld - fromWorld;
        var maxDist = delta.Length();
        if (maxDist <= 0f) return 0f;
        var dir = delta / maxDist;
        var mask = (int)(CollisionGroup.Impassable | CollisionGroup.Opaque);
        var ray = new Robust.Shared.Physics.CollisionRay(fromWorld, dir, mask);
        var hits = _physics.IntersectRay(mapId, ray, maxDist, selfGrid, returnOnFirstHit: false);
        float nearest = float.MaxValue;
        foreach (var hit in hits)
        {
            if (hit.HitEntity == selfGrid) continue;
            if (TryComp<TransformComponent>(hit.HitEntity, out var hx) && hx.GridUid == selfGrid) continue;
            if (hit.Distance > 0f && hit.Distance < nearest) nearest = hit.Distance;
        }
        return nearest == float.MaxValue ? 0f : nearest;
    }

    private bool TryGetEnemyShuttleConsolePos(EntityUid enemyGrid, out Vector2 worldPos)
    {
        var q = EntityQueryEnumerator<ShuttleConsoleComponent, TransformComponent>();
        while (q.MoveNext(out var consoleUid, out _, out var cxform))
        {
            if (cxform.GridUid == enemyGrid)
            {
                worldPos = _xform.GetWorldPosition(cxform);
                return true;
            }
        }
        worldPos = default;
        return false;
    }
}
