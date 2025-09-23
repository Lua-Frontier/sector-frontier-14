// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Server.Shuttles.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Controllers;
using System.Numerics;
using Content.Shared.Shuttles.Components;
using Content.Server.Gateway.Components;
using Content.Server.Salvage.Expeditions;

namespace Content.Server._Lua.Physics.Controllers;

internal sealed class OuterLimitController : VirtualController
{
    private const float Slowdown2 = 30000f * 30000f;
    private const float Stop2 = 30100f * 30100f;

    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        base.UpdateBeforeSolve(prediction, frameTime);
        var shuttleQuery = EntityQueryEnumerator<MapGridComponent, ShuttleComponent, PhysicsComponent, TransformComponent>();
        while (shuttleQuery.MoveNext(out var uid, out var grid, out var shuttle, out var physics, out var xform))
        {
            if (Paused(uid)) continue;
            if (physics.BodyType != BodyType.Dynamic) continue;
            if (!physics.Awake && physics.LinearVelocity == Vector2.Zero) continue;
            if (HasComp<FTLComponent>(uid)) continue;
            if (xform.MapUid != null && HasComp<FTLMapComponent>(xform.MapUid.Value)) continue;
            if (HasComp<GatewayGeneratorDestinationComponent>(uid) || HasComp<SalvageExpeditionComponent>(uid)) continue;
            if (xform.MapUid != null && (HasComp<GatewayGeneratorDestinationComponent>(xform.MapUid.Value) || HasComp<SalvageExpeditionComponent>(xform.MapUid.Value))) continue;
            var worldPos = TransformSystem.GetWorldPosition(xform);
            var dist2 = worldPos.LengthSquared();
            if (dist2 >= Stop2)
            {
                PhysicsSystem.SetLinearVelocity(uid, Vector2.Zero, body: physics);
                PhysicsSystem.SetAngularVelocity(uid, 0f, body: physics); continue;
            }
            if (dist2 >= Slowdown2)
            {
                var vel = physics.LinearVelocity;
                var speed2 = vel.LengthSquared();
                if (speed2 > 1f)
                { var speed = MathF.Sqrt(speed2); PhysicsSystem.SetLinearVelocity(uid, vel / speed, body: physics); }
            }
        }
    }
}


