using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server._Goobstation.SpaceWhale;

public sealed class TailedEntitySystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly SharedJointSystem _joint = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TailedEntityComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<TailedEntityComponent, ComponentShutdown>(OnComponentShutdown);
    }

    private void OnComponentStartup(EntityUid uid, TailedEntityComponent component, ComponentStartup args)
    {
        if (component.TailSegments.Count == 0)
            InitializeTailSegments((uid, component, Transform(uid)));
    }

    private void OnComponentShutdown(EntityUid uid, TailedEntityComponent component, ComponentShutdown args)
    {
        foreach (var segment in component.TailSegments)
        {
            if (Exists(segment) && !EntityManager.IsQueuedForDeletion(segment))
            {
                _joint.ClearJoints(segment);
                Del(segment);
            }
        }
        component.TailSegments.Clear();
    }

    public override void Update(float frameTime)
    {
        CleanupOrphanSegments();
        var query = EntityQueryEnumerator<TailedEntityComponent, TransformComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform, out var headBody))
        {
            if (!EnsureTailIntact(uid, comp, xform))
                continue;

            if (comp.KinematicFollow)
                ApplyKinematicFollow(uid, comp, xform, headBody, frameTime);
            else
                ApplyWiggle(uid, comp, xform, frameTime);
        }
    }

    private bool EnsureTailIntact(EntityUid uid, TailedEntityComponent comp, TransformComponent xform)
    {
        var changed = false;
        for (var i = comp.TailSegments.Count - 1; i >= 0; i--)
        {
            var segment = comp.TailSegments[i];
            if (Exists(segment) && !EntityManager.IsQueuedForDeletion(segment))
                continue;

            comp.TailSegments.RemoveAt(i);
            changed = true;
        }

        if (comp.TailSegments.Count == comp.Amount && !changed)
            return true;

        foreach (var segment in comp.TailSegments)
        {
            if (!Exists(segment) || EntityManager.IsQueuedForDeletion(segment))
                continue;
            _joint.ClearJoints(segment);
            QueueDel(segment);
        }

        comp.TailSegments.Clear();
        InitializeTailSegments((uid, comp, xform));
        return false;
    }

    private void ApplyKinematicFollow(
        EntityUid uid,
        TailedEntityComponent comp,
        TransformComponent headXform,
        PhysicsComponent headBody,
        float frameTime)
    {
        var mapUid = headXform.MapUid;
        if (mapUid == null)
            return;

        var headVel = headBody.LinearVelocity;
        for (var i = 0; i < comp.TailSegments.Count; i++)
        {
            var segUid = comp.TailSegments[i];
            if (TryComp<PhysicsComponent>(segUid, out var coastBody))
                _physics.SetLinearVelocity(segUid, headVel, body: coastBody);
        }

        var interval = MathF.Max(comp.FollowInterval, 0.05f);
        comp.FollowAccumulator += frameTime;
        if (comp.FollowAccumulator < interval)
            return;

        while (comp.FollowAccumulator >= interval)
            comp.FollowAccumulator -= interval;

        var headPos = _transformSystem.GetWorldPosition(headXform);
        var headRot = _transformSystem.GetWorldRotation(headXform);

        var baseLerp = Math.Clamp(comp.FollowLerp <= 0f ? 1f : comp.FollowLerp, 0.05f, 1f);
        var leaderPos = headPos;
        var leaderRot = headRot;

        for (var i = 0; i < comp.TailSegments.Count; i++)
        {
            var segUid = comp.TailSegments[i];
            if (!TryComp<TransformComponent>(segUid, out var segXform))
                continue;

            var curPos = _transformSystem.GetWorldPosition(segXform);
            var toLeader = leaderPos - curPos;
            var dist2 = toLeader.LengthSquared();

            Vector2 idealPos;
            Angle idealRot;
            if (dist2 > 0.0001f)
            {
                var dir = toLeader / MathF.Sqrt(dist2);
                idealPos = leaderPos - dir * comp.Spacing;
                idealRot = MathF.Atan2(dir.Y, dir.X);
            }
            else
            {
                var headFwd = leaderRot.ToWorldVec();
                if (headFwd.LengthSquared() < 0.0001f)
                    headFwd = Vector2.UnitX;
                else
                    headFwd = headFwd.Normalized();

                idealPos = leaderPos - headFwd * comp.Spacing;
                idealRot = leaderRot;
            }
            var lerp = Math.Clamp(baseLerp / (1f + i * MathF.Max(comp.FollowLerpFalloff, 0f)), 0.05f, 1f);
            var newPos = lerp >= 0.999f ? idealPos : Vector2.Lerp(curPos, idealPos, lerp);

            _transformSystem.SetCoordinates(
                segUid,
                segXform,
                new EntityCoordinates(mapUid.Value, newPos),
                rotation: idealRot);

            if (TryComp<TailedEntitySegmentComponent>(segUid, out var segComp))
                segComp.Index = i;

            leaderPos = newPos;
            leaderRot = idealRot;
        }
    }

    private void ApplyWiggle(EntityUid uid, TailedEntityComponent comp, TransformComponent headXform, float frameTime)
    {
        if (comp.WiggleAmplitude <= 0f || comp.WiggleFrequency <= 0f)
            return;
        var time = (float) _timing.CurTime.TotalSeconds;
        var headPos = _transformSystem.GetWorldPosition(headXform);
        var headFwd = _transformSystem.GetWorldRotation(headXform).ToWorldVec();
        var headPerp = new Vector2(-headFwd.Y, headFwd.X);
        var prevPos = headPos;
        for (var i = 0; i < comp.TailSegments.Count; i++)
        {
            var segUid = comp.TailSegments[i];
            if (!TryComp<TransformComponent>(segUid, out var segXform))
                continue;
            if (!TryComp<PhysicsComponent>(segUid, out var body))
                continue;
            var segPos = _transformSystem.GetWorldPosition(segXform);
            var dir = prevPos - segPos;
            Vector2 perp;
            var len2 = dir.LengthSquared();
            if (len2 > 0.0001f)
            {
                dir /= MathF.Sqrt(len2);
                perp = new Vector2(-dir.Y, dir.X);
            }
            else
            {
                perp = headPerp;
            }
            var phase = time * (MathF.Tau * comp.WiggleFrequency) - i * 0.35f;
            var s = MathF.Sin(phase);
            var magnitude = comp.Stiffness * comp.WiggleAmplitude * 2.0f;
            if ((body.BodyType & BodyType.KinematicController) != 0)
            {
                var impulse = perp * (s * magnitude * frameTime);
                _physics.ApplyLinearImpulse(segUid, impulse, body: body);
            }
            else
            {
                var force = perp * (s * magnitude);
                _physics.ApplyForce(segUid, force, body: body);
            }
            prevPos = segPos;
        }
    }

    private void InitializeTailSegments(Entity<TailedEntityComponent, TransformComponent> ent)
    {
        var (uid, comp, xform) = ent;
        var mapUid = xform.MapUid;
        if (mapUid == null)
            return;
        if (!HasComp<PhysicsComponent>(uid))
            return;
        var headPos = _transformSystem.GetWorldPosition(xform);
        var headRot = _transformSystem.GetWorldRotation(xform);
        var headFwd = headRot.ToWorldVec();
        if (headFwd.LengthSquared() < 0.0001f)
            headFwd = Vector2.UnitX;
        else
            headFwd = headFwd.Normalized();

        comp.TailSegments.Clear();
        comp.FollowAccumulator = 0f;
        for (var i = 0; i < comp.Amount; i++)
        {
            var spawnPos = headPos - headFwd * (comp.Spacing * (i + 1));
            var segment = Spawn(comp.Prototype, new EntityCoordinates(mapUid.Value, spawnPos));
            _transformSystem.SetCoordinates(
                segment,
                Transform(segment),
                new EntityCoordinates(mapUid.Value, spawnPos),
                rotation: headRot);

            var segComp = EnsureComp<TailedEntitySegmentComponent>(segment);
            segComp.HeadEntity = uid;
            segComp.Index = i;
            comp.TailSegments.Add(segment);
        }

        if (comp.KinematicFollow)
            return;

        var prev = uid;
        foreach (var segment in comp.TailSegments)
        {
            if (!HasComp<PhysicsComponent>(segment))
                continue;
            var joint = _joint.CreateDistanceJoint(bodyA: prev, bodyB: segment, anchorA: comp.AnchorAOffset, anchorB: comp.AnchorBOffset, minimumDistance: comp.Spacing * 0.8f);
            joint.Length = comp.Spacing;
            joint.MinLength = comp.Spacing * comp.MinLengthMultiplier;
            joint.MaxLength = comp.Spacing * comp.MaxLengthMultiplier;
            joint.Stiffness = comp.Stiffness;
            joint.Damping = comp.Damping;
            joint.ID = $"TailJoint_{prev}_{segment}";
            prev = segment;
        }
    }

    private void CleanupOrphanSegments()
    {
        var query = EntityQueryEnumerator<TailedEntitySegmentComponent>();
        while (query.MoveNext(out var uid, out var seg))
        {
            if (!Exists(seg.HeadEntity) || EntityManager.IsQueuedForDeletion(seg.HeadEntity))
            {
                _joint.ClearJoints(uid);
                QueueDel(uid);
                continue;
            }
            if (!HasComp<TailedEntityComponent>(seg.HeadEntity))
            {
                _joint.ClearJoints(uid);
                QueueDel(uid);
            }
        }
    }
}
