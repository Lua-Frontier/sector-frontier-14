// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using Content.Server.Temperature.Systems;
using Content.Shared._Lua.SpaceHazards;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server._Lua.SpaceHazards;

public sealed class SectorStarHazardSystem : EntitySystem
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);
    private const int BaseMaxHits = 12;
    private const int BaseBareTileClears = 8;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SpaceHazardActivitySystem _activity = default!;
    [Dependency] private readonly TemperatureSystem _temperature = default!;
    private TimeSpan _nextTick;
    private readonly List<Entity<MapGridComponent>> _gridScratch = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        if (now < _nextTick) return;
        _nextTick = now + TickInterval;
        foreach (var uid in _activity.ActiveHazards)
        {
            if (!TryComp(uid, out SectorCelestialBodyComponent? body) || body.Kind != CelestialKind.Star) continue;
            if (!TryComp(uid, out TransformComponent? xform) || xform.MapID == MapId.Nullspace) continue;
            var pos = _transform.GetWorldPosition(xform);
            var hazardR = MathF.Max(body.HazardRadius, 1f);
            var box = Box2.CenteredAround(pos, new Vector2(hazardR * 2f, hazardR * 2f));
            var grids = _gridScratch;
            grids.Clear();
            _mapManager.FindGridsIntersecting(xform.MapID, box, ref grids, approx: true, includeMap: false);
            foreach (var grid in grids)
            {
                var gridUid = grid.Owner;
                var dist = DistanceToGrid(gridUid, grid.Comp, pos);
                if (dist > hazardR) continue;
                var factor = SectorCelestialProximity.Factor(dist, hazardR);
                var hits = SectorCelestialProximity.ScaledHits(BaseMaxHits, factor);
                if (hits > 0)
                { SectorCelestialHullDamage.DamageInRadius(gridUid, grid.Comp, pos, hazardR, body.StarDamage * factor, hits, _maps, _turf, _damageable, EntityManager, _random); }
                var clears = SectorCelestialProximity.ScaledHits(BaseBareTileClears, factor);
                SectorCelestialHullDamage.ClearBareTilesInRadius(gridUid, grid.Comp, pos, hazardR, clears, _maps, _turf, EntityManager, _random);
            }
            SectorCelestialMobDamage.ApplyHazardToMobsInRadius(xform.MapID, pos, hazardR, body.MobHazardDamage, _lookup, _transform, _damageable, _temperature, EntityManager);
        }
    }

    private float DistanceToGrid(EntityUid gridUid, MapGridComponent grid, Vector2 worldPoint)
    {
        var inv = _transform.GetInvWorldMatrix(gridUid);
        var local = Vector2.Transform(worldPoint, inv);
        var aabb = grid.LocalAABB;
        var closest = Vector2.Clamp(local, aabb.BottomLeft, aabb.TopRight);
        var closestWorld = Vector2.Transform(closest, _transform.GetWorldMatrix(gridUid));
        return (closestWorld - worldPoint).Length();
    }
}
