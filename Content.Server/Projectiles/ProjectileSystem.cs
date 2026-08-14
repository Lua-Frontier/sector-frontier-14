using Content.Server.Destructible;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;

namespace Content.Server.Projectiles;

public sealed partial class ProjectileSystem : SharedProjectileSystem
{
    [Dependency] private readonly DestructibleSystem _destructibleSystem = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;

    private EntityQuery<PhysicsComponent> _physQuery;
    private EntityQuery<FixturesComponent> _fixQuery;

    public override void Initialize()
    {
        base.Initialize();

        _physQuery = GetEntityQuery<PhysicsComponent>();
        _fixQuery = GetEntityQuery<FixturesComponent>();

        UpdatesBefore.Add(typeof(SharedPhysicsSystem));
    }

    public override DamageSpecifier? ProjectileCollide(Entity<ProjectileComponent, PhysicsComponent> projectile, EntityUid target, MapCoordinates? collisionCoordinates, bool predicted = false)
    {
        var (uid, component, _) = projectile;

        if (component.ProjectileSpent)
            return null;

        var damageRequired = FixedPoint2.Zero;
        if (TryComp<DamageableComponent>(target, out var damageableComponent))
        {
            damageRequired = _destructibleSystem.DestroyedAt(target);
            damageRequired -= damageableComponent.TotalDamage;
            damageRequired = FixedPoint2.Max(damageRequired, FixedPoint2.Zero);
        }

        var modifiedDamage = base.ProjectileCollide(projectile, target, collisionCoordinates, predicted);

        if (modifiedDamage == null)
        {
            if (!component.NoDamageDelete)
                return null;

            var spEv = new ProjectileSpentEvent();
            RaiseLocalEvent(uid, spEv);

            component.ProjectileSpent = true;
            if (component.DeleteOnCollide)
                QueueDel(uid);

            return null;
        }

        if (component.PenetrationThreshold != 0)
        {
            if (component.PenetrationDamageTypeRequirement != null)
            {
                var stopPenetration = false;
                foreach (var requiredDamageType in component.PenetrationDamageTypeRequirement)
                {
                    if (!modifiedDamage.DamageDict.ContainsKey(requiredDamageType))
                    {
                        stopPenetration = true;
                        break;
                    }
                }

                if (stopPenetration)
                    component.ProjectileSpent = true;
            }

            if (modifiedDamage.GetTotal() < damageRequired)
                component.ProjectileSpent = true;

            if (!component.ProjectileSpent)
            {
                component.PenetrationAmount += damageRequired;
                if (component.PenetrationAmount >= component.PenetrationThreshold)
                    component.ProjectileSpent = true;
            }
        }
        else
        {
            component.ProjectileSpent = true;
        }

        if (component.ProjectileSpent)
        {
            var spEv = new ProjectileSpentEvent();
            RaiseLocalEvent(uid, spEv);

            if (component.DeleteOnCollide)
                QueueDel(uid);
        }

        return modifiedDamage;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ProjectileComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var projectileComp, out var physicsComp))
        {
            if (projectileComp.ProjectileSpent || TerminatingOrDeleted(uid))
                continue;

            var xform = Transform(uid);
            var currentVelocity = projectileComp.RaycastResetVelocity ?? _physics.GetMapLinearVelocity(uid, physicsComp, xform);
            var velLen = currentVelocity.Length();
            if (!ShouldRaycastProjectile(velLen) && projectileComp.RaycastResetVelocity == null)
                continue;

            var lastMap = _transformSystem.GetMapCoordinates(xform);
            var lastPosition = lastMap.Position;
            var rayDirection = currentVelocity / velLen;
            var rayDistance = velLen * frameTime;
            if (rayDistance <= 0f)
                continue;

            if (!_fixQuery.TryComp(uid, out var fix) || !fix.Fixtures.TryGetValue(ProjectileFixture, out var projFix))
                continue;

            var hits = _physics.IntersectRay(xform.MapID,
                new CollisionRay(lastPosition, rayDirection, projFix.CollisionMask),
                rayDistance,
                uid,
                false);

            if (!ProcessHits(hits) && projectileComp.RaycastResetVelocity is { } resetVel)
            {
                var parentVel = _physics.GetMapLinearVelocity(xform.ParentUid);
                var resetTo = resetVel - parentVel;
                _physics.SetLinearVelocity(uid, resetTo, body: physicsComp);
                projectileComp.RaycastResetVelocity = null;
            }

            bool ProcessHits(IEnumerable<RayCastResults> hitResults)
            {
                (EntityUid? Uid, float Distance) minHit = (null, float.MaxValue);
                foreach (var hit in hitResults)
                {
                    var hitEnt = hit.HitEntity;

                    if (!_physQuery.TryComp(hitEnt, out var otherBody) || !_fixQuery.TryComp(hitEnt, out var otherFix))
                        continue;

                    Fixture? hitFix = null;
                    foreach (var kv in otherFix.Fixtures)
                    {
                        if (kv.Value.Hard)
                        {
                            hitFix = kv.Value;
                            break;
                        }
                    }

                    if (hitFix == null)
                        continue;

                    var ourEv = new PreventCollideEvent(uid, hitEnt, physicsComp, otherBody, projFix, hitFix);
                    RaiseLocalEvent(uid, ref ourEv);
                    if (ourEv.Cancelled)
                        continue;

                    var otherEv = new PreventCollideEvent(hitEnt, uid, otherBody, physicsComp, hitFix, projFix);
                    RaiseLocalEvent(hitEnt, ref otherEv);
                    if (otherEv.Cancelled)
                        continue;

                    if (hit.Distance < minHit.Distance)
                        minHit = (hitEnt, hit.Distance);
                }

                if (minHit.Uid == null)
                    return false;

                var hitXform = Transform(minHit.Uid.Value);
                var hitMapCoord = lastMap.Offset(rayDirection * minHit.Distance);
                var hitPos = _transformSystem.ToCoordinates(hitMapCoord);
                if (hitXform.Coordinates.EntityId != hitXform.GridUid && hitXform.GridUid != null)
                    hitPos = _transformSystem.WithEntityId(hitPos, hitXform.GridUid.Value);

                if (projectileComp.RaycastResetVelocity == null)
                {
                    var parentVel = _physics.GetMapLinearVelocity(xform.ParentUid);
                    projectileComp.RaycastResetVelocity = currentVelocity + parentVel;
                    var curVel = physicsComp.LinearVelocity;
                    curVel.Normalize();
                    curVel *= 1f / frameTime;
                    _physics.SetLinearVelocity(uid, curVel, body: physicsComp);
                }

                _transformSystem.SetCoordinates(uid, hitPos);

                return true;
            }
        }
    }
}
