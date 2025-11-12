// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using System.Numerics;
using JetBrains.Annotations;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Map.Components;

namespace Content.Server._Lua.Physics;

[UsedImplicitly]
public sealed class AutoUnstuckSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    private readonly Dictionary<EntityUid, float> _stuckTime = new();
    private readonly List<EntityUid> _awakeOwners = new();
    private readonly List<EntityUid> _pendingRemovals = new();
    private const int RemovalBudgetPerTick = 10000;
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
        SubscribeLocalEvent<PhysicsComponent, EntityTerminatingEvent>(OnEntityTerminating);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var processed = 0;
        for (var i = _pendingRemovals.Count - 1; i >= 0 && processed < RemovalBudgetPerTick; i--, processed++)
        {
            var uid = _pendingRemovals[i];
            _pendingRemovals.RemoveAt(i);
            _stuckTime.Remove(uid);
        }
        var toClear = new List<EntityUid>();
        _awakeOwners.Clear();
        foreach (var ent in _physics.AwakeBodies) { _awakeOwners.Add(ent.Owner); }
        foreach (var uid in _awakeOwners)
        {
            if (!Exists(uid))
            { toClear.Add(uid); continue; }
            if (!_physicsQuery.TryGetComponent(uid, out var body)) continue;
            if (HasComp<MapGridComponent>(uid)) { toClear.Add(uid); continue; }
            if (body.BodyType == BodyType.Static || !body.CanCollide) continue;
            var hasStaticHardContact = false;
            var dirSum = Vector2.Zero;
            var contacts = _physics.GetContacts(uid);
            while (contacts.MoveNext(out var contact))
            {
                if (!contact.IsTouching || !contact.Hard) continue;
                var other = contact.OtherEnt(uid);
                var otherBody = contact.OtherBody(uid);
                if (otherBody.BodyType != BodyType.Static) continue;
                var selfTx = _physics.GetPhysicsTransform(uid);
                var otherTx = _physics.GetPhysicsTransform(other);
                var dir = selfTx.Position - otherTx.Position;
                if (IsFinite(dir) && dir != Vector2.Zero) dirSum += Vector2.Normalize(dir);
                hasStaticHardContact = true;
            }
            if (!hasStaticHardContact)
            { toClear.Add(uid); continue; }
            if (_stuckTime.TryGetValue(uid, out var t)) _stuckTime[uid] = t + frameTime;
            else _stuckTime[uid] = frameTime;
            if (_stuckTime[uid] < 15f) continue;
            if (dirSum != Vector2.Zero)
            {
                var pushDir = Vector2.Normalize(dirSum);
                if (!IsFinite(pushDir)) { toClear.Add(uid); continue; }
                if (_xformQuery.TryGetComponent(uid, out var xform))
                {
                    _physics.SetCanCollide(uid, false, body: body);
                    var delta = pushDir * 2f;
                    var newPos = xform.WorldPosition + delta;
                    if (IsFinite(newPos)) _xform.SetWorldPosition(uid, newPos);
                    _physics.SetCanCollide(uid, true, body: body);
                    var vel = pushDir * 1.0f;
                    _physics.SetLinearVelocity(uid, vel, body: body);
                    _physics.WakeBody(uid, body: body);
                }
            }
            toClear.Add(uid);
        }
        foreach (var uid in toClear)
        { _stuckTime.Remove(uid); }
    }

    private void OnEntityTerminating(Entity<PhysicsComponent> ent, ref EntityTerminatingEvent args)
    { _pendingRemovals.Add(ent.Owner); }

    private static bool IsFinite(Vector2 v) => float.IsFinite(v.X) && float.IsFinite(v.Y);
}


