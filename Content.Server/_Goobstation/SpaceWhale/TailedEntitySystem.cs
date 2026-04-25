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
        var query = EntityQueryEnumerator<TailedEntityComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            var validCount = 0;
            foreach (var segment in comp.TailSegments)
            { if (Exists(segment) && !EntityManager.IsQueuedForDeletion(segment)) validCount++; }
            if (validCount == comp.Amount && comp.TailSegments.Count == comp.Amount)
            {
                ApplyWiggle(uid, comp, xform, frameTime);
                continue;
            }
            foreach (var segment in comp.TailSegments)
            {
                if (Exists(segment) && !EntityManager.IsQueuedForDeletion(segment))
                {
                    _joint.ClearJoints(segment);
                    QueueDel(segment);
                }
            }
            comp.TailSegments.Clear();
            InitializeTailSegments((uid, comp, xform));
        }
    }

    private void ApplyWiggle(EntityUid uid, TailedEntityComponent comp, TransformComponent headXform, float frameTime)
    {
        if (comp.WiggleAmplitude <= 0f || comp.WiggleFrequency <= 0f) return;
        var time = (float) _timing.CurTime.TotalSeconds;
        var headPos = _transformSystem.GetWorldPosition(headXform);
        var headFwd = _transformSystem.GetWorldRotation(headXform).ToWorldVec();
        var headPerp = new Vector2(-headFwd.Y, headFwd.X);
        var prevPos = headPos;
        for (var i = 0; i < comp.TailSegments.Count; i++)
        {
            var segUid = comp.TailSegments[i];
            if (!TryComp<TransformComponent>(segUid, out var segXform)) continue;
            if (!TryComp<PhysicsComponent>(segUid, out var body)) continue;
            var segPos = _transformSystem.GetWorldPosition(segXform);
            var dir = prevPos - segPos;
            Vector2 perp;
            var len2 = dir.LengthSquared();
            if (len2 > 0.0001f)
            {
                dir /= MathF.Sqrt(len2);
                perp = new Vector2(-dir.Y, dir.X);
            }
            else { perp = headPerp; }
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
        if (mapUid == null) return;
        if (!HasComp<PhysicsComponent>(uid)) return;
        var headPos = _transformSystem.GetWorldPosition(xform);
        var headRot = _transformSystem.GetWorldRotation(xform);
        comp.TailSegments.Clear();
        for (var i = 0; i < comp.Amount; i++)
        {
            var offset = headRot.ToWorldVec() * comp.Spacing * (i + 1);
            var spawnPos = headPos - offset;
            var segment = Spawn(comp.Prototype, new EntityCoordinates(mapUid.Value, spawnPos));
            var segComp = EnsureComp<TailedEntitySegmentComponent>(segment);
            segComp.HeadEntity = uid;
            segComp.Index = i;
            comp.TailSegments.Add(segment);
        }
        var prev = uid;
        foreach (var segment in comp.TailSegments)
        {
            if (!HasComp<PhysicsComponent>(segment)) continue;
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
