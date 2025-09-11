using Content.Shared._Lua.AiShuttle;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using System.Numerics;

namespace Content.Server._Lua.AiShuttle;

public partial class AiShuttleBrainSystem
{
    private void ProcessPatrolBehavior(EntityUid uid, ref AiShuttleBrainComponent brain, TransformComponent xform, PhysicsComponent body, Vector2 pos, Vector2 sectorCenter, float controlDt)
    {
        var offset = pos - sectorCenter;
        var r = offset.Length();
        var outward = offset == Vector2.Zero ? Vector2.UnitX : Vector2.Normalize(offset);
        if (brain.PatrolHoldTimer > 0f) brain.PatrolHoldTimer = MathF.Max(0f, brain.PatrolHoldTimer - controlDt);
        if (brain.PatrolHolding && brain.PatrolHoldTimer <= 0f)
        {
            brain.PatrolHolding = false;
            brain.PatrolWaypoint = null;
        }
        if (brain.PatrolWaypoint == null && brain.PatrolHoldTimer <= 0f)
        { GeneratePatrolWaypoint(uid, ref brain, pos, sectorCenter); }
        if (brain.PatrolWaypoint is { } wp)
        { ProcessPatrolWaypoint(uid, ref brain, xform, body, pos, wp, sectorCenter, outward, controlDt); }
    }

    private void GeneratePatrolWaypoint(EntityUid uid, ref AiShuttleBrainComponent brain, Vector2 pos, Vector2 sectorCenter)
    {
        var rng = Random.Shared;
        Vector2 sample;
        if (brain.PatrolSectorEnabled)
        {
            var theta = (float)(rng.NextDouble() * Math.PI * 2.0);
            var rr = (float)Math.Sqrt(rng.NextDouble()) * brain.PatrolSectorRadius;
            sample = sectorCenter + new Vector2(MathF.Cos(theta), MathF.Sin(theta)) * rr;
        }
        else
        {
            var theta = (float)(rng.NextDouble() * Math.PI * 2.0);
            var dirRnd = new Vector2(MathF.Cos(theta), MathF.Sin(theta));
            var dist = MathHelper.Clamp((float)(brain.PatrolFtlMinDistance + rng.NextDouble() * (brain.PatrolFtlMaxDistance - brain.PatrolFtlMinDistance)), brain.PatrolFtlMinDistance, brain.PatrolFtlMaxDistance);
            sample = pos + dirRnd * dist;
        }
        brain.PatrolWaypoint = sample;
    }
    private void ProcessPatrolWaypoint(EntityUid uid, ref AiShuttleBrainComponent brain, TransformComponent xform, PhysicsComponent body, Vector2 pos, Vector2 wp, Vector2 sectorCenter, Vector2 outward, float controlDt)
    {
        var toWp = wp - pos;
        var distWp = toWp.Length();
        if (distWp > brain.PatrolWaypointTolerance)
        {
            var dirTo = distWp > 0f ? toWp / distWp : Vector2.UnitX;
            brain.FacingMarker = wp;
            var (avoid, forwardHit, leftClear, rightClear) = ComputeAvoidance(uid, pos, dirTo, xform.MapID, null, isPatrolMode: true);
            var fwdSpeed = MathF.Max(0f, Vector2.Dot(body.LinearVelocity, dirTo));
            var safeStop = MathF.Max(brain.ObstacleForwardStopDistance, fwdSpeed * brain.ObstacleStopVelocityMultiplier + 50f);
            var blockedAhead = forwardHit < float.MaxValue && forwardHit < safeStop;
            var emergencyStop = forwardHit < float.MaxValue && forwardHit < 30f;
            if (blockedAhead)
            { HandlePatrolObstacle(uid, ref brain, body, dirTo, fwdSpeed, safeStop, emergencyStop, forwardHit, leftClear, rightClear, controlDt); }
            else
            { HandlePatrolMovement(uid, ref brain, xform, body, dirTo, distWp, controlDt); }
            brain.PatrolLastDistToWaypoint = distWp;
        }
        else if (brain.PatrolSectorEnabled)
        { HandlePatrolArrival(uid, ref brain, body, wp, controlDt); }
        else if (_shuttle.CanFTL(uid, out _))
        { ExecutePatrolFTL(uid, ref brain, xform, pos, controlDt); }
        else
        { HandlePatrolIdle(uid, ref brain, body, outward, controlDt); }
    }

    private void HandlePatrolObstacle(EntityUid uid, ref AiShuttleBrainComponent brain, PhysicsComponent body, Vector2 dirTo, float fwdSpeed, float safeStop, bool emergencyStop, float forwardHit, float leftClear, float rightClear, float controlDt)
    {
        var preferRight = rightClear >= leftClear;
        var sidePrefer = preferRight ? 1 : -1;
        var brakeForce = emergencyStop ? 1f : MathF.Min(1f, fwdSpeed / 10f);
        DriveViaConsole(uid, brain, body, controlDt, Vector2.Zero, Vector2.Zero, brakeForce, true, sidePrefer, gateForwardOnYaw: false);
        brain.PatrolBlockedTimer += controlDt;
        if (brain.PatrolBlockedTimer >= brain.PatrolBlockedResetSeconds)
        { brain.PatrolWaypoint = null; brain.PatrolBlockedTimer = 0f; }
    }

    private void HandlePatrolMovement(EntityUid uid, ref AiShuttleBrainComponent brain, TransformComponent xform, PhysicsComponent body, Vector2 dirTo, float distWp, float controlDt)
    {
        if (brain.PatrolLastDistToWaypoint == 0f || distWp < brain.PatrolLastDistToWaypoint - 0.5f) brain.PatrolBlockedTimer = 0f;
        if (brain.WingSlot == 0)
        { HandleWingLeaderPatrolMovement(uid, ref brain, xform, body, dirTo, controlDt); }
        else
        { DriveViaConsole(uid, brain, body, controlDt, dirTo, Vector2.Zero, 2f, false, 0, gateForwardOnYaw: true); }
    }

    private void HandleWingLeaderPatrolMovement(EntityUid uid, ref AiShuttleBrainComponent brain, TransformComponent xform, PhysicsComponent body, Vector2 dirTo, float controlDt)
    {
        var maxWingmanDistance = 0f;
        var wingQuery = EntityQueryEnumerator<AiShuttleBrainComponent, TransformComponent>();
        while (wingQuery.MoveNext(out var wingUid, out var wingBrain, out var wingXform))
        {
            if (wingBrain.WingLeader != uid) continue;
            if (wingBrain.WingSlot <= 0) continue;
            if (wingXform.MapID != xform.MapID) continue;

            var wingmanPos = _xform.GetWorldPosition(wingXform);
            var distToWingman = (_xform.GetWorldPosition(xform) - wingmanPos).Length();
            maxWingmanDistance = MathF.Max(maxWingmanDistance, distToWingman);
        }
        if (maxWingmanDistance > 72f)
        {
            var currentSpeed = body.LinearVelocity.Length();
            var forwardSpeed = MathF.Max(0f, Vector2.Dot(body.LinearVelocity, dirTo));
            if (forwardSpeed > 10f)
            { DriveViaConsole(uid, brain, body, controlDt, dirTo, Vector2.Zero, 1f, true, 0, gateForwardOnYaw: true); }
            else
            { DriveViaConsole(uid, brain, body, controlDt, dirTo, Vector2.Zero, 0.5f, false, 0, gateForwardOnYaw: true); }
        }
        else
        { DriveViaConsole(uid, brain, body, controlDt, dirTo, Vector2.Zero, 2f, false, 0, gateForwardOnYaw: true); }
    }

    private void HandlePatrolArrival(EntityUid uid, ref AiShuttleBrainComponent brain, PhysicsComponent body, Vector2 wp, float controlDt)
    {
        if (!brain.PatrolHolding)
        {
            brain.PatrolHolding = true;
            brain.PatrolHoldTimer = brain.PatrolHoldSeconds;
        }
        if (brain.KeepFacingOnHold) brain.FacingMarker = wp;
        else brain.FacingMarker = null;
        DriveViaConsole(uid, brain, body, controlDt, Vector2.Zero, Vector2.Zero, 0f, true, 0, gateForwardOnYaw: false);
        brain.PatrolBlockedTimer = 0f;
        brain.PatrolLastDistToWaypoint = 0f;
    }

    private void ExecutePatrolFTL(EntityUid uid, ref AiShuttleBrainComponent brain, TransformComponent xform, Vector2 pos, float controlDt)
    {
        var rng = Random.Shared;
        var theta = (float)(rng.NextDouble() * Math.PI * 2.0);
        var dir = new Vector2(MathF.Cos(theta), MathF.Sin(theta));
        var dist = MathHelper.Clamp((float)(brain.PatrolFtlMinDistance + rng.NextDouble() * (brain.PatrolFtlMaxDistance - brain.PatrolFtlMinDistance)), brain.PatrolFtlMinDistance, brain.PatrolFtlMaxDistance);
        var arrive = pos + dir * dist;
        var coords = new MapCoordinates(arrive, xform.MapID);
        brain.StabilizeTimer = MathF.Max(brain.StabilizeTimer, brain.PostFtlStabilizeSeconds);
        _shuttle.FTLToCoordinates(uid, Comp<Server.Shuttles.Components.ShuttleComponent>(uid), _xform.ToCoordinates(coords), Angle.Zero);
        brain.PatrolWaypoint = null;
    }

    private void HandlePatrolIdle(EntityUid uid, ref AiShuttleBrainComponent brain, PhysicsComponent body, Vector2 outward, float controlDt)
    {
        var tangent = new Vector2(-outward.Y, outward.X);
        DriveViaConsole(uid, brain, body, controlDt, Vector2.Zero, tangent, 0f, false, 0);
    }
}
