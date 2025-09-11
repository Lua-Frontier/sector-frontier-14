using Content.Shared._Lua.AiShuttle;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using System.Linq;
using System.Numerics;

namespace Content.Server._Lua.AiShuttle;

public partial class AiShuttleBrainSystem
{
    private void AssignAiShuttleWings(MapId mapId)
    {
        var aiShuttles = new List<(EntityUid uid, AiShuttleBrainComponent brain, TransformComponent xform)>();
        var q = EntityQueryEnumerator<AiShuttleBrainComponent, TransformComponent>();
        while (q.MoveNext(out var uid, out var brain, out var xform))
        {
            if (xform.MapID != mapId)
                continue;
            if (!brain.Enabled)
                continue;
            aiShuttles.Add((uid, brain, xform));
        }
        var shipsInWings = aiShuttles.Count(s => s.brain.WingSlot >= 0 && !s.brain.FormingWing && (s.brain.WingSlot == 0 || (s.brain.WingLeader != null && !Deleted(s.brain.WingLeader.Value))) && !s.brain.InCombat);
        var freeShips = aiShuttles.Count(s => (s.brain.WingSlot < 0 || s.brain.FormingWing || (s.brain.WingSlot > 0 && (s.brain.WingLeader == null || Deleted(s.brain.WingLeader.Value)))) && !s.brain.InCombat);
        var shipsInCombat = aiShuttles.Count(s => s.brain.InCombat);
        if (shipsInCombat > 0)
        { return; }
        if (freeShips < 3)
        {
            if (shipsInWings == 0 && freeShips > 0)
            {
                foreach (var (uid, brain, _) in aiShuttles.Where(s => (s.brain.WingSlot < 0 || s.brain.FormingWing || (s.brain.WingSlot > 0 && (s.brain.WingLeader == null || Deleted(s.brain.WingLeader.Value)))) && !s.brain.InCombat))
                {
                    brain.WingLeader = null;
                    brain.WingSlot = -1;
                    brain.FormingWing = false;
                }
            }
            else if (shipsInWings > 0)
            { }
            return;
        }
        var freeShuttles = aiShuttles.Where(s => (s.brain.WingSlot < 0 || s.brain.FormingWing || (s.brain.WingSlot > 0 && (s.brain.WingLeader == null || Deleted(s.brain.WingLeader.Value)))) && !s.brain.InCombat).ToList();
        if (freeShuttles.Count == 3)
        {
            foreach (var (uid, brain, _) in freeShuttles)
            {
                brain.WingLeader = null;
                brain.WingSlot = -1;
                brain.FormingWing = true;
                brain.PatrolWaypoint = null;
                brain.PatrolHolding = false;
                brain.PatrolHoldTimer = 0f;
            }
            for (int i = 0; i + 2 < freeShuttles.Count; i += 3)
            {
                var a = freeShuttles[i];
                var b = freeShuttles[i + 1];
                var c = freeShuttles[i + 2];
                var leader = a.uid;
                var leaderBrain = a.brain;
                leaderBrain.WingLeader = leader;
                leaderBrain.WingSlot = 0;
                leaderBrain.FormingWing = true;
                b.brain.WingLeader = leader;
                b.brain.WingSlot = 1;
                b.brain.FormingWing = true;
                c.brain.WingLeader = leader;
                c.brain.WingSlot = 2;
                c.brain.FormingWing = true;
            }
        }
        else if (freeShuttles.Count > 3)
        {
            foreach (var (uid, brain, _) in freeShuttles)
            {
                brain.WingLeader = null;
                brain.WingSlot = -1;
                brain.FormingWing = true;
                brain.PatrolWaypoint = null;
                brain.PatrolHolding = false;
                brain.PatrolHoldTimer = 0f;
            }
            for (int i = 0; i + 2 < freeShuttles.Count; i += 3)
            {
                var a = freeShuttles[i];
                var b = freeShuttles[i + 1];
                var c = freeShuttles[i + 2];
                var leader = a.uid;
                var leaderBrain = a.brain;
                leaderBrain.WingLeader = leader;
                leaderBrain.WingSlot = 0;
                leaderBrain.FormingWing = true;
                b.brain.WingLeader = leader;
                b.brain.WingSlot = 1;
                b.brain.FormingWing = true;
                c.brain.WingLeader = leader;
                c.brain.WingSlot = 2;
                c.brain.FormingWing = true;
            }
            var remainingStart = (freeShuttles.Count / 3) * 3;
            for (int i = remainingStart; i < freeShuttles.Count; i++)
            { freeShuttles[i].brain.FormingWing = true; }
        }
        else
        {
            foreach (var (uid, brain, _) in freeShuttles)
            {
                brain.WingLeader = null;
                brain.WingSlot = -1;
                brain.FormingWing = false;
            }
        }
    }

    private void ProcessWingmanBehavior(EntityUid uid, ref AiShuttleBrainComponent brain, TransformComponent xform, PhysicsComponent body, float controlDt, EntityUid? targetGrid, Vector2 targetPos, Vector2 aimPos)
    {
        if (brain.WingSlot > 0 && brain.WingLeader is { } leader && !Deleted(leader) && TryComp<TransformComponent>(leader, out var lXform) && TryComp<AiShuttleBrainComponent>(leader, out var lBrain))
        {
            var leaderPos = _xform.GetWorldPosition(lXform);
            var leaderRot = _xform.GetWorldRotation(lXform) + Angle.FromDegrees(lBrain.ForwardAngleOffset);
            var backDistance = lBrain.FormationSpacing * 0.8f;
            var sideDistance = lBrain.FormationSpacing * 0.6f;
            var forward = leaderRot.RotateVec(Vector2.UnitY);
            var right = leaderRot.RotateVec(Vector2.UnitX);
            Vector2 formTarget;
            if (brain.WingSlot == 1)
            { formTarget = leaderPos - forward * backDistance - right * sideDistance; }
            else
            { formTarget = leaderPos - forward * backDistance + right * sideDistance; }
            if (lBrain.CurrentTarget != null)
            {
                targetGrid = lBrain.CurrentTarget;
                targetPos = lBrain.LastKnownTargetPos ?? _xform.GetWorldPosition(Transform(lBrain.CurrentTarget.Value));
                aimPos = lBrain.FacingMarker ?? lBrain.LastKnownTargetPos ?? targetPos;
                brain.FacingMarker = aimPos;
            }
            var pos = _xform.GetWorldPosition(xform);
            var toForm = formTarget - pos;
            var distForm = toForm.Length();
            if (Math.Abs(brain.PatrolBlockedTimer - MathF.Round(brain.PatrolBlockedTimer)) < 1e-3f) { }
            const float minFormDist = 64f;
            const float maxFormDist = 72f;
            var distToLeader = (leaderPos - pos).Length();
            var inFormationRange = distToLeader >= minFormDist && distToLeader <= maxFormDist;
            Vector2 dirTo;
            Vector2 tangential = Vector2.Zero;
            float radialError = 0f;
            bool requestBrake = false;
            if (inFormationRange)
            { dirTo = Vector2.Zero; requestBrake = true; brain.FacingMarker = leaderPos + forward * 100f; }
            else if (distToLeader < minFormDist)
            {
                var awayFromLeader = (pos - leaderPos);
                if (awayFromLeader.Length() > 0f)
                { dirTo = awayFromLeader / awayFromLeader.Length(); }
                else
                { dirTo = Vector2.UnitX; }
                brain.FacingMarker = leaderPos + forward * 100f;
                var (avoidDir, forwardHit, leftClear, rightClear) = ComputeAvoidance(uid, pos, dirTo, xform.MapID, null, isPatrolMode: true);
                var fwdSpeed = MathF.Max(0f, Vector2.Dot(body.LinearVelocity, dirTo));
                var safeStop = MathF.Max(brain.ObstacleForwardStopDistance, fwdSpeed * brain.ObstacleStopVelocityMultiplier + 50f);
                var blockedAhead = forwardHit < float.MaxValue && forwardHit < safeStop;
                var emergencyStop = forwardHit < float.MaxValue && forwardHit < 30f;
                var currentSpeed = body.LinearVelocity.Length();
                var isStuck = currentSpeed < 5f && distToLeader < minFormDist + 10f;
                if (isStuck)
                {
                    brain.PatrolBlockedTimer += controlDt;
                    if (brain.PatrolBlockedTimer > 3f)
                    {
                        var aroundLeader = new Vector2(-awayFromLeader.Y, awayFromLeader.X);
                        if (rightClear > leftClear) aroundLeader = -aroundLeader;
                        dirTo = aroundLeader / MathF.Max(aroundLeader.Length(), 0.001f);
                    }
                }
                else
                { brain.PatrolBlockedTimer = 0f; }
                if (blockedAhead || isStuck)
                {
                    var preferRight = rightClear > leftClear;
                    var sideDir = preferRight ? new Vector2(-dirTo.Y, dirTo.X) : new Vector2(dirTo.Y, -dirTo.X);
                    if (isStuck)
                    {
                        brain.PatrolBlockedTimer += controlDt;
                        if (brain.PatrolBlockedTimer > 2f)
                        {
                            var toLeader = (leaderPos - pos).Normalized();
                            var aroundLeader = new Vector2(-toLeader.Y, toLeader.X);
                            if (rightClear > leftClear) aroundLeader = -aroundLeader;
                            dirTo = aroundLeader * 0.8f + sideDir * 0.2f;
                        }
                        else if (brain.PatrolBlockedTimer > 1f)
                        { dirTo = -dirTo * 0.3f + sideDir * 0.7f; }
                        else
                        { dirTo = sideDir * 0.8f + dirTo * 0.2f; }
                    }
                    else if (leftClear < 15f && rightClear < 15f)
                    { dirTo = -dirTo * 0.3f + sideDir * 0.7f; }
                    else if (leftClear < 30f && rightClear < 30f)
                    { dirTo = sideDir * 0.6f + dirTo * 0.4f; }
                    else
                    { dirTo = sideDir * 0.7f + dirTo * 0.3f; }
                    tangential = new Vector2(-dirTo.Y, dirTo.X) * 0.5f;
                    radialError = 0f;
                    requestBrake = emergencyStop && !isStuck;
                    if (currentSpeed > 5f) brain.PatrolBlockedTimer = 0f;
                }
                else
                {
                    tangential = new Vector2(-dirTo.Y, dirTo.X) * 0.2f;
                    radialError = 0f;
                    requestBrake = false;
                }
            }
            else
            {
                dirTo = distForm > 0f ? toForm / MathF.Max(distForm, 0.001f) : Vector2.Zero;
                brain.FacingMarker = leaderPos + forward * 100f;
                var currentSpeed = body.LinearVelocity.Length();
                var forwardSpeed = MathF.Max(0f, Vector2.Dot(body.LinearVelocity, dirTo));
                if (distToLeader > 200f)
                {
                    radialError = 2f;
                    requestBrake = false;
                }
                else if (distToLeader > 100f)
                {
                    if (forwardSpeed > 15f)
                    { radialError = 0.5f; requestBrake = true; }
                    else
                    { radialError = 1.5f; requestBrake = false; }
                }
                else
                {
                    if (forwardSpeed > 8f)
                    { radialError = 0.3f; requestBrake = true; }
                    else
                    { radialError = 1f; requestBrake = false; }
                }
                var (avoidDir, forwardHit, leftClear, rightClear) = ComputeAvoidance(uid, pos, dirTo, xform.MapID, null, isPatrolMode: true);
                var fwdSpeed = MathF.Max(0f, Vector2.Dot(body.LinearVelocity, dirTo));
                var safeStop = MathF.Max(brain.ObstacleForwardStopDistance, fwdSpeed * brain.ObstacleStopVelocityMultiplier + 50f);
                var blockedAhead = forwardHit < float.MaxValue && forwardHit < safeStop;
                var emergencyStop = forwardHit < float.MaxValue && forwardHit < 30f;
                var isStuck = currentSpeed < 3f && distForm > 20f;
                if (blockedAhead || isStuck)
                {
                    var preferRight = rightClear > leftClear;
                    var sideDir = preferRight ? new Vector2(-dirTo.Y, dirTo.X) : new Vector2(dirTo.Y, -dirTo.X);
                    if (isStuck)
                    {
                        brain.PatrolBlockedTimer += controlDt;
                        if (brain.PatrolBlockedTimer > 2f)
                        {
                            var toLeader = (leaderPos - pos).Normalized();
                            var aroundLeader = new Vector2(-toLeader.Y, toLeader.X);
                            if (rightClear > leftClear) aroundLeader = -aroundLeader;
                            dirTo = aroundLeader * 0.8f + sideDir * 0.2f;
                        }
                        else if (brain.PatrolBlockedTimer > 1f)
                        { dirTo = -dirTo * 0.6f + sideDir * 0.4f; }
                        else
                        { dirTo = sideDir * 0.9f + dirTo * 0.1f; }
                    }
                    else if (leftClear < 15f && rightClear < 15f)
                    { dirTo = -dirTo * 0.7f + sideDir * 0.3f; }
                    else if (leftClear < 30f && rightClear < 30f)
                    { dirTo = sideDir * 0.6f + dirTo * 0.4f; }
                    else
                    { dirTo = sideDir * 0.8f + dirTo * 0.2f; }
                    tangential = new Vector2(-dirTo.Y, dirTo.X) * 0.5f;
                    radialError = 0f;
                    requestBrake = emergencyStop && !isStuck;
                    if (currentSpeed > 5f) brain.PatrolBlockedTimer = 0f;
                }
                else
                {
                    tangential = new Vector2(-dirTo.Y, dirTo.X) * 0.1f;
                    brain.PatrolBlockedTimer = 0f;
                }
            }
            DriveViaConsole(uid, brain, body, controlDt, dirTo, tangential, radialError, requestBrake, 0);
            if (targetGrid != null)
            {
                var fireAt = aimPos == default ? targetPos : aimPos;
                if ((fireAt - pos).Length() <= brain.MaxWeaponRange + 1f) _fire.TryAimAndFireGrid(uid, targetGrid.Value, fireAt);
            }
        }
    }
}
