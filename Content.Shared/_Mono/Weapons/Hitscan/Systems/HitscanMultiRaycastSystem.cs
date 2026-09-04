using Content.Shared._Mono.Weapons.Hitscan.Components;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Weapons.Hitscan.Events;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._Mono.Weapons.Hitscan.Systems;

public sealed partial class HitscanMultiRaycastSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly ISharedAdminLogManager _log = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    private EntityQuery<PhysicsComponent> _physicQuery;
    private HashSet<EntityUid> _hitEntities = [];

    public override void Initialize()
    {
        base.Initialize();
        _physicQuery = GetEntityQuery<PhysicsComponent>();
        SubscribeLocalEvent<HitscanMultiRaycastComponent, HitscanTraceEvent>(OnHitscanFired);
    }

    private void OnHitscanFired(Entity<HitscanMultiRaycastComponent> ent, ref HitscanTraceEvent args)
    {
        var shooter = args.Shooter ?? args.Gun;
        var mapCords = _transform.ToMapCoordinates(args.FromCoordinates);
        var ray = new CollisionRay(mapCords.Position, args.ShotDirection, (int) ent.Comp.CollisionMask);
        var rayCastResults = _physics.IntersectRay(mapCords.MapId, ray, ent.Comp.MaxDistance, shooter, false);
        var hitCount = 0;
        var latestDistance = ent.Comp.MaxDistance;

        foreach (var result in rayCastResults)
        {
            if (!_physicQuery.TryComp(result.HitEntity, out var phys))
                continue;

            _hitEntities.Add(result.HitEntity);
            latestDistance = result.Distance;

            hitCount++;

            if (hitCount > ent.Comp.MaxPierce)
                break;

            if ((phys.CollisionLayer & (int) ent.Comp.PierceCollisionMask) != 0x0)
                break;

            _log.Add(LogType.HitScanHit,
                $"{ToPrettyString(shooter):user} hit {ToPrettyString(result.HitEntity):target}"
                + $" using {ToPrettyString(args.Gun):entity}.");
        }

        var trace = new HitscanRaycastFiredEvent
        {
            FromCoordinates = args.FromCoordinates,
            ShotDirection = args.ShotDirection,
            Gun = args.Gun,
            Shooter = args.Shooter,
            HitEntities = _hitEntities,
            DistanceTried = latestDistance,
        };

        RaiseLocalEvent(ent, ref trace);
        _hitEntities.Clear();
    }
}
