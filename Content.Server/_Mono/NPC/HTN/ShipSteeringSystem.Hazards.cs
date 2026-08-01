// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Shared._Lua.AmbientSpaceEffects;
using Content.Shared._Lua.SpaceHazards;
using Content.Shared.Shuttles.BUIStates;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Mono.NPC.HTN;

public sealed partial class ShipSteeringSystem
{
    private const float HazardWaypointArrive = 120f;
    private const float HazardWaypointPassRadius = 360f;
    private const float HazardClearance = 60f;
    private const int HazardCircleSamples = 32;
    private const int MaxRouteNodes = 640;
    private const float CorridorExtraWidth = 2500f;
    private static readonly TimeSpan HazardCacheTtl = TimeSpan.FromMinutes(10);
    [Dependency] private readonly IGameTiming _timing = default!;
    private readonly Dictionary<MapId, (TimeSpan BuiltAt, List<(Vector2 Center, float Radius)> Circles)> _hazardCache = new();
    private readonly List<(Vector2 Center, float Radius)> _routeHazards = new();
    private readonly List<Vector2> _routeNodes = new();
    private readonly List<int> _routePrev = new();
    private readonly List<float> _routeDist = new();
    private readonly List<bool> _routeUsed = new();

    public void InvalidateHazardCache(MapId? mapId = null)
    {
        if (mapId is { } id) _hazardCache.Remove(id);
        else _hazardCache.Clear();
    }

    public void PlanHazardWaypoints(EntityUid pilot, ShipSteererComponent comp)
    {
        comp.Waypoints = null;
        comp.WaypointIndex = 0;
        var pilotXform = Transform(pilot);
        var shipUid = pilotXform.GridUid;
        if (shipUid == null || !_gridQuery.TryComp(shipUid, out var shipGrid)) return;
        var shipPos = _transform.GetMapCoordinates(Transform(shipUid.Value));
        var dest = _transform.ToMapCoordinates(comp.Coordinates);
        if (shipPos.MapId != dest.MapId || shipPos.MapId == MapId.Nullspace) return;
        var hazards = GetCachedHazardCircles(shipPos.MapId);
        if (hazards.Count == 0) return;
        var pad = shipGrid.LocalAABB.Size.Length() * 0.5f + HazardClearance;
        var start = shipPos.Position;
        var goal = dest.Position;
        CollectCorridorHazards(hazards, pad, start, goal, _routeHazards);
        if (_routeHazards.Count == 0) return;
        if (!SegmentHitsHazards(start, goal, _routeHazards, goal, allowGoalHazard: false)) return;
        if (!TryBuildHazardRoute(start, goal, _routeHazards, out var waypoints)) return;
        if (waypoints.Count == 0) return;
        SimplifyWaypoints(waypoints, start, goal, _routeHazards);
        if (waypoints.Count == 0) return;
        comp.Waypoints = waypoints;
        comp.WaypointIndex = 0;
    }

    private static void CollectCorridorHazards(List<(Vector2 Center, float Radius)> hazards, float pad,  Vector2 start, Vector2 goal, List<(Vector2 Center, float Radius)> into)
    {
        into.Clear();
        var corridorSlack = CorridorExtraWidth;
        foreach (var (center, radius) in hazards)
        {
            var r = radius + pad;
            if (PointInCircle(start, center, r) || PointInCircle(goal, center, r) || DistPointToSegment(center, start, goal) <= r + corridorSlack)
            { into.Add((center, r)); }
        }
    }

    private bool TryBuildHazardRoute(Vector2 start, Vector2 goal, List<(Vector2 Center, float Radius)> hazards, out List<Vector2> waypoints)
    {
        waypoints = new List<Vector2>();
        _routeNodes.Clear();
        _routeNodes.Add(start); // 0
        _routeNodes.Add(goal);  // 1
        foreach (var (center, radius) in hazards)
        {
            for (var i = 0; i < HazardCircleSamples; i++)
            {
                if (_routeNodes.Count >= MaxRouteNodes) break;
                var ang = i * MathF.Tau / HazardCircleSamples;
                var p = center + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * radius;
                if (IsBuriedInOtherHazard(p, center, hazards)) continue;
                _routeNodes.Add(p);
            }
        }
        var n = _routeNodes.Count;
        if (n <= 2) return false;
        _routeDist.Clear();
        _routePrev.Clear();
        _routeUsed.Clear();
        for (var i = 0; i < n; i++)
        {
            _routeDist.Add(float.PositiveInfinity);
            _routePrev.Add(-1);
            _routeUsed.Add(false);
        }
        _routeDist[0] = 0f;
        for (var iter = 0; iter < n; iter++)
        {
            var best = -1;
            var bestD = float.PositiveInfinity;
            for (var i = 0; i < n; i++)
            {
                if (_routeUsed[i] || _routeDist[i] >= bestD) continue;
                bestD = _routeDist[i];
                best = i;
            }
            if (best < 0 || bestD == float.PositiveInfinity) break;
            _routeUsed[best] = true;
            if (best == 1) break;
            var from = _routeNodes[best];
            for (var j = 0; j < n; j++)
            {
                if (_routeUsed[j] || j == best) continue;
                var to = _routeNodes[j];
                var toIsGoal = j == 1;
                if (SegmentHitsHazards(from, to, hazards, goal, allowGoalHazard: toIsGoal)) continue;
                var w = (to - from).Length();
                var nd = bestD + w;
                if (nd >= _routeDist[j]) continue;
                _routeDist[j] = nd;
                _routePrev[j] = best;
            }
        }
        if (_routePrev[1] < 0 && _routeDist[1] == float.PositiveInfinity) return false;
        var chain = new List<Vector2>();
        for (var cur = 1; cur >= 0; cur = _routePrev[cur])
        {
            chain.Add(_routeNodes[cur]);
            if (cur == 0) break;
            if (_routePrev[cur] < 0) return false;
        }
        chain.Reverse();
        for (var i = 1; i < chain.Count - 1; i++) waypoints.Add(chain[i]);
        return true;
    }

    private static void SimplifyWaypoints(List<Vector2> waypoints, Vector2 start, Vector2 goal, List<(Vector2 Center, float Radius)> hazards)
    {
        if (waypoints.Count < 2) return;
        var simplified = new List<Vector2>(waypoints.Count);
        var prev = start;

        for (var i = 0; i < waypoints.Count; i++)
        {
            var candidate = waypoints[i];
            var next = i + 1 < waypoints.Count ? waypoints[i + 1] : goal;
            if (!SegmentHitsHazards(prev, next, hazards, goal, allowGoalHazard: false)) continue;
            simplified.Add(candidate);
            prev = candidate;
        }
        waypoints.Clear();
        waypoints.AddRange(simplified);
    }

    private static bool IsBuriedInOtherHazard(Vector2 p, Vector2 ownCenter, List<(Vector2 Center, float Radius)> hazards)
    {
        foreach (var (center, radius) in hazards)
        {
            if ((center - ownCenter).LengthSquared() < 0.01f) continue;
            if ((p - center).LengthSquared() < radius * radius * 0.85f * 0.85f) return true;
        }
        return false;
    }

    private static bool SegmentHitsHazards(Vector2 from, Vector2 to, List<(Vector2 Center, float Radius)> hazards, Vector2 goal, bool allowGoalHazard)
    {
        foreach (var (center, radius) in hazards)
        {
            if (allowGoalHazard && PointInCircle(goal, center, radius)) continue;
            if (PointInCircle(from, center, radius - 1f))
            {
                if (ApproxOnRing(to, center, radius)) continue;
                return true;
            }
            if (ApproxOnRing(from, center, radius) && ApproxOnRing(to, center, radius))
            {
                var dAng = AbsoluteAngleDelta(from - center, to - center);
                var step = MathF.Tau / HazardCircleSamples + 0.08f;
                if (dAng <= step) continue;
                return true;
            }
            if (SegmentIntersectsCircle(from, to, center, radius)) return true;
        }
        return false;
    }

    private static bool ApproxOnRing(Vector2 p, Vector2 c, float r, float tol = 12f)
    { return MathF.Abs((p - c).Length() - r) <= tol; }

    private static float AbsoluteAngleDelta(Vector2 a, Vector2 b)
    {
        if (a.LengthSquared() < 0.0001f || b.LengthSquared() < 0.0001f) return MathF.Tau;
        var angA = MathF.Atan2(a.Y, a.X);
        var angB = MathF.Atan2(b.Y, b.X);
        var d = MathF.Abs(angA - angB);
        return MathF.Min(d, MathF.Tau - d);
    }

    private static bool PointInCircle(Vector2 p, Vector2 c, float r)
    { return (p - c).LengthSquared() <= r * r; }

    private static float DistPointToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var lenSq = ab.LengthSquared();
        if (lenSq < 0.0001f) return (p - a).Length();
        var t = Math.Clamp(Vector2.Dot(p - a, ab) / lenSq, 0f, 1f);
        return (a + ab * t - p).Length();
    }

    private List<(Vector2 Center, float Radius)> GetCachedHazardCircles(MapId mapId)
    {
        var now = _timing.CurTime;
        if (_hazardCache.TryGetValue(mapId, out var entry) && now - entry.BuiltAt < HazardCacheTtl) return entry.Circles;
        var circles = entry.Circles ?? new List<(Vector2 Center, float Radius)>();
        RebuildHazardCircles(mapId, circles);
        _hazardCache[mapId] = (now, circles);
        return circles;
    }

    private void RebuildHazardCircles(MapId mapId, List<(Vector2 Center, float Radius)> into)
    {
        into.Clear();
        var celestial = EntityQueryEnumerator<SectorCelestialBodyComponent, TransformComponent>();
        while (celestial.MoveNext(out _, out var body, out var xform))
        {
            if (xform.MapID != mapId) continue;
            var pos = _transform.GetWorldPosition(xform);
            var radius = body.Kind switch
            {
                CelestialKind.BlackHole => MathF.Max(body.PullRadius, body.HazardRadius),
                _ => MathF.Max(body.RadiationRange, body.HazardRadius),
            };
            if (radius > 1f) into.Add((pos, radius));
        }
        var fields = EntityQueryEnumerator<AmbientSpaceFieldComponent, TransformComponent>();
        while (fields.MoveNext(out _, out var field, out var xform))
        {
            if (xform.MapID != mapId || !field.HasWeather) continue;
            var pos = _transform.GetWorldPosition(xform);
            var radius = MathF.Max(field.Radius, 1f);
            into.Add((pos, radius));
        }
    }

    private void AdvanceHazardWaypoints(ShipSteererComponent comp, Vector2 shipPos, Vector2 linVel)
    {
        if (comp.Waypoints == null) return;
        while (comp.WaypointIndex < comp.Waypoints.Count)
        {
            var toWp = comp.Waypoints[comp.WaypointIndex] - shipPos;
            var dist = toWp.Length();
            if (dist <= HazardWaypointArrive)
            {
                comp.WaypointIndex++;
                continue;
            }
            if (dist <= HazardWaypointPassRadius && linVel.LengthSquared() > 1f && Vector2.Dot(linVel, toWp) < 0f)
            {
                comp.WaypointIndex++;
                continue;
            }
            break;
        }
    }

    private static MapCoordinates GetSteerMapTarget(ShipSteererComponent comp, MapCoordinates finalTarget, MapId mapId)
    {
        if (comp.Waypoints != null && comp.WaypointIndex < comp.Waypoints.Count)
            return new MapCoordinates(comp.Waypoints[comp.WaypointIndex], mapId);
        return finalTarget;
    }

    private static bool HasPendingWaypoints(ShipSteererComponent comp)
    { return comp.Waypoints != null && comp.WaypointIndex < comp.Waypoints.Count; }

    private void AppendHazardObstacles(MapId mapId, Vector2 shipPos, float maxDist)
    {
        var hazards = GetCachedHazardCircles(mapId);
        if (hazards.Count == 0) return;
        foreach (var (center, radius) in hazards)
        {
            var limit = maxDist + radius + 64f;
            if ((center - shipPos).LengthSquared() > limit * limit) continue;
            _avoidEnts.Add(new ObstacleCandidate(center, radius + HazardClearance, true, Vector2.Zero));
        }
    }

    public List<DroneRouteState>? BuildDroneRoutes(MapId? mapFilter = null, HashSet<EntityUid>? steererFilter = null)
    {
        List<DroneRouteState>? routes = null;
        var query = EntityQueryEnumerator<ShipSteererComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var steerer, out var xform))
        {
            if (steererFilter != null && !steererFilter.Contains(uid)) continue;
            var shipUid = xform.GridUid;
            if (shipUid == null) continue;
            var shipXform = Transform(shipUid.Value);
            var shipMap = _transform.GetMapCoordinates(shipXform);
            if (mapFilter != null && shipMap.MapId != mapFilter.Value) continue;
            if (shipMap.MapId == MapId.Nullspace) continue;
            var mapUid = _mapMan.GetMapEntityId(shipMap.MapId);
            var points = new List<NetCoordinates>
            { GetNetCoordinates(new EntityCoordinates(mapUid, shipMap.Position)), };
            if (steerer.Waypoints != null)
            { for (var i = steerer.WaypointIndex; i < steerer.Waypoints.Count; i++) points.Add(GetNetCoordinates(new EntityCoordinates(mapUid, steerer.Waypoints[i]))); }
            points.Add(GetNetCoordinates(steerer.Coordinates));
            routes ??= new List<DroneRouteState>();
            routes.Add(new DroneRouteState(GetNetEntity(uid), points));
        }
        return routes;
    }

    public bool HasActiveSteerers()
    { return Count<ShipSteererComponent>() > 0; }

    private static bool SegmentIntersectsCircle(Vector2 a, Vector2 b, Vector2 c, float r)
    {
        var ab = b - a;
        var lenSq = ab.LengthSquared();
        if (lenSq < 0.0001f) return (a - c).LengthSquared() <= r * r;
        var t = Math.Clamp(Vector2.Dot(c - a, ab) / lenSq, 0f, 1f);
        var closest = a + ab * t;
        return (closest - c).LengthSquared() <= r * r;
    }
}
