using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Shared._Mono.Weapons.Hitscan.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Hitscan.Systems;
using Robust.Shared.Map;

namespace Content.Shared._Mono.Weapons.Hitscan.Systems;

public sealed partial class HitscanJumpSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    private EntityQuery<MobThresholdsComponent> _mobQuery;

    public override void Initialize()
    {
        base.Initialize();
        _mobQuery = GetEntityQuery<MobThresholdsComponent>();
        SubscribeLocalEvent<HitscanJumpComponent, HitscanRaycastFiredEvent>(OnHitscanHit, after: [typeof(HitscanReflectSystem)]);
    }

    private void OnHitscanHit(Entity<HitscanJumpComponent> ent, ref HitscanRaycastFiredEvent args)
    {
        if (args.Canceled ||
            args.HitEntities.Count == 0 ||
            args.Shooter == null ||
            !_mobQuery.HasComp(args.HitEntities.First()) ||
            ent.Comp.Count <= 0)
            return;
        ent.Comp.IgnoredEntities.Add(args.Shooter.Value);
        ent.Comp.IgnoredEntities.Add(args.HitEntities.First());
        var fromCoords = Transform(args.HitEntities.First()).Coordinates;
        if (!GetClosestTarget(fromCoords, ent.Comp.Range, ent.Comp.IgnoredEntities, out _, out var delta))
            return;
        ent.Comp.Count -= 1;
        var hitFire = new HitscanTraceEvent
        {
            FromCoordinates = fromCoords,
            ShotDirection = -Vector2.Normalize(delta.Value),
            Gun = args.Gun,
            Shooter = args.HitEntities.First(),
        };
        RaiseLocalEvent(ent, ref hitFire);
    }

    private bool GetClosestTarget(EntityCoordinates coords, float range, HashSet<EntityUid> ignoredEnts, [NotNullWhen(true)] out EntityUid? closest, [NotNullWhen(true)] out Vector2? delta)
    {
        var eqe = _lookup.GetEntitiesInRange<MobStateComponent>(coords, range);
        delta = null;
        closest = null;
        var cD = range;
        foreach (var ent in eqe)
        {
            if (ignoredEnts.Contains(ent.Owner))
                continue;
            coords.TryDistance(EntityManager, Transform(ent).Coordinates, out var d);
            if (cD > d)
            {
                cD = d;
                closest = ent.Owner;
            }
        }
        if (closest.HasValue)
            delta = _transform.ToWorldPosition(coords) - _transform.ToWorldPosition(Transform(closest.Value).Coordinates);
        return closest.HasValue;
    }
}
