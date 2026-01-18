// LuaWorld/LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld/LuaCorp
// See AGPLv3.txt for details.

using Content.Server.Worldgen.Components.GC;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using System.Linq;
using System.Numerics;

namespace Content.Server._Lua.Worldgen.Systems.GC;

public sealed class GCAbleVelocityDampSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    private const float DampDurationSeconds = 5f;
    private const float EndFactor = 0.01f;
    private static readonly float DampK = MathF.Log(1f / EndFactor) / DampDurationSeconds;
    private readonly Dictionary<EntityUid, float> _dampRemaining = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GCAbleObjectComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<GCAbleObjectComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(EntityUid uid, GCAbleObjectComponent component, ComponentShutdown args)
    { _dampRemaining.Remove(uid); }

    private void OnStartCollide(Entity<GCAbleObjectComponent> ent, ref StartCollideEvent args)
    {
        if (!args.OurFixture.Hard || !args.OtherFixture.Hard) return;
        _dampRemaining[ent.Owner] = DampDurationSeconds;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_dampRemaining.Count == 0) return;
        var keys = _dampRemaining.Keys.ToArray();
        foreach (var uid in keys)
        {
            if (Deleted(uid) || Terminating(uid))
            {
                _dampRemaining.Remove(uid);
                continue;
            }
            if (!TryComp<PhysicsComponent>(uid, out var body))
            {
                _dampRemaining.Remove(uid);
                continue;
            }
            var remaining = _dampRemaining[uid] - frameTime;
            if (remaining <= 0f)
            {
                _physics.SetLinearVelocity(uid, Vector2.Zero, body: body);
                _physics.SetAngularVelocity(uid, 0f, body: body);
                _dampRemaining.Remove(uid);
                continue;
            }
            _dampRemaining[uid] = remaining;
            var factor = MathF.Exp(-DampK * frameTime);
            var newLin = body.LinearVelocity * factor;
            var newAng = body.AngularVelocity * factor;
            if (newLin.LengthSquared() < 0.0001f) newLin = Vector2.Zero;
            if (MathF.Abs(newAng) < 0.01f)  newAng = 0f;
            _physics.SetLinearVelocity(uid, newLin, body: body);
            _physics.SetAngularVelocity(uid, newAng, body: body);
        }
    }
}


