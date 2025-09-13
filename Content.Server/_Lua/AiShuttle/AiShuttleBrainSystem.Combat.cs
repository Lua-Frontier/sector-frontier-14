// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.
using Content.Shared._Lua.AiShuttle;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using System.Numerics;

namespace Content.Server._Lua.AiShuttle;

public partial class AiShuttleBrainSystem
{
    private void ProcessCombatBehavior(EntityUid uid, ref AiShuttleBrainComponent brain, TransformComponent xform, PhysicsComponent body, Vector2 pos, EntityUid targetGrid, Vector2 targetPos, Vector2 aimPos, float controlDt)
    {
        if (!TryGetEnemyGunneryServerPos(targetGrid, out var stillAim))
        {
            brain.CurrentTarget = null;
            brain.LastKnownTargetPos = null;
            brain.FacingMarker = null;
            return;
        }
        aimPos = stillAim;
        var toTarget = targetPos - pos;
        var distCenter = toTarget.Length();
        var hullDist = ComputeHullDistance(uid, pos, targetPos, xform.MapID, targetGrid);
        var dist = hullDist > 0f ? hullDist : distCenter;
        if (dist < brain.MinSeparation && dist > 0f)
        { HandleCombatSeparation(uid, ref brain, body, toTarget, dist, controlDt); return; }
        var ftlThreshold = MathF.Max(brain.FtlMinDistance, brain.MaxWeaponRange * 3f);
        if (dist > ftlThreshold && brain.FtlCooldownTimer <= 0f && _shuttle.CanFTL(uid, out _))
        { ExecuteCombatFTL(uid, ref brain, xform, targetPos, toTarget, controlDt); return; }
        ProcessCombatOrbit(uid, ref brain, xform, body, pos, targetPos, aimPos, targetGrid, toTarget, dist, controlDt);
    }

    private void HandleCombatSeparation(EntityUid uid, ref AiShuttleBrainComponent brain, PhysicsComponent body, Vector2 toTarget, float dist, float controlDt)
    {
        var away = -toTarget / dist;
        DriveViaConsole(uid, brain, body, controlDt, toTarget / MathF.Max(dist, 0.001f), Vector2.Zero, -MathF.Max(0f, brain.MinSeparation - dist), true, 0);
    }

    private void ExecuteCombatFTL(EntityUid uid, ref AiShuttleBrainComponent brain, TransformComponent xform, Vector2 targetPos, Vector2 toTarget, float controlDt)
    {
        Vector2 dirVel = Vector2.Zero;
        if (TryComp<PhysicsComponent>(brain.CurrentTarget!.Value, out var targetBody)) dirVel = targetBody.LinearVelocity;
        var dir = dirVel.LengthSquared() > 0.01f ? Vector2.Normalize(dirVel) : (toTarget == Vector2.Zero ? Vector2.UnitY : Vector2.Normalize(toTarget));
        var arrive = targetPos - dir * brain.FtlExitOffset;
        var coords = new MapCoordinates(arrive, xform.MapID);
        brain.StabilizeTimer = MathF.Max(brain.StabilizeTimer, brain.PostFtlStabilizeSeconds);
        brain.FtlCooldownTimer = brain.FtlCooldownSeconds;
        _shuttle.FTLToCoordinates(uid, Comp<Content.Server.Shuttles.Components.ShuttleComponent>(uid), _xform.ToCoordinates(coords), Angle.Zero);
    }

    private void ProcessCombatOrbit(EntityUid uid, ref AiShuttleBrainComponent brain, TransformComponent xform, PhysicsComponent body, Vector2 pos, Vector2 targetPos, Vector2 aimPos, EntityUid targetGrid, Vector2 toTarget, float dist, float controlDt)
    {
        var dirTo = dist > 0f ? toTarget / dist : Vector2.UnitX;
        var tangential = new Vector2(-dirTo.Y, dirTo.X);
        var radialError = ((brain.FightRangeMin + brain.FightRangeMax) * 0.5f) - dist;
        var desiredDir = tangential * 0.9f - dirTo * Math.Clamp(radialError * 0.5f, -1f, 1f);
        var exclude = targetGrid;
        var (avoidDir, forwardHit, leftClear, rightClear) = ComputeAvoidance(uid, pos, desiredDir, xform.MapID, exclude, isPatrolMode: false);
        if (avoidDir != Vector2.Zero)
        {
            desiredDir = Vector2.Normalize(avoidDir) * 1.2f + desiredDir * 0.8f;
            dirTo = desiredDir.LengthSquared() > 0.0001f ? Vector2.Normalize(desiredDir) : dirTo;
            tangential = new Vector2(-dirTo.Y, dirTo.X);
        }
        if (forwardHit > 0f && forwardHit < 12f)
        {
            desiredDir -= dirTo * 3.5f;
            dirTo = desiredDir.LengthSquared() > 0.0001f ? Vector2.Normalize(desiredDir) : dirTo;
            tangential = new Vector2(-dirTo.Y, dirTo.X);
        }
        var radialForInput = radialError;
        if (brain.StabilizeTimer > 0f) radialForInput *= 0.35f;
        else radialForInput *= 1.2f;
        var requestBrake = false;
        var sidePrefer = 0;
        if (forwardHit > 0f && forwardHit < 16f)
        {
            var sideSign = leftClear >= rightClear ? 1f : -1f;
            sidePrefer = sideSign >= 0f ? 1 : -1;
            var side = new Vector2(-dirTo.Y, dirTo.X) * sideSign;
            desiredDir = side - dirTo * 0.8f;
            dirTo = desiredDir.LengthSquared() > 0.0001f ? Vector2.Normalize(desiredDir) : dirTo;
            tangential = new Vector2(-dirTo.Y, dirTo.X);
            radialForInput = MathF.Min(0f, radialForInput);
            requestBrake = true;
        }
        DriveViaConsole(uid, brain, body, controlDt, dirTo, tangential, radialForInput, requestBrake, sidePrefer);
        var fireAt = aimPos == default ? targetPos : aimPos;
        if ((fireAt - pos).Length() <= brain.MaxWeaponRange + 1f)
            _fire.TryAimAndFireGrid(uid, targetGrid, fireAt);
    }

    private void HandleCombatModeTransition(EntityUid uid, ref AiShuttleBrainComponent brain, bool hasTarget)
    {
        if (hasTarget)
        {
            if (!brain.InCombat)
            {
                brain.InCombat = true;
                ResetWingFormation(uid, ref brain);
            }
        }
        else
        {
            if (brain.InCombat)
            {
                brain.InCombat = false;
            }
        }
    }
}
