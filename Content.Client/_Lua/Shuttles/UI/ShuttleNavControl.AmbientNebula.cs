// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Client._Lua.AmbientSpaceEffects;
using Content.Shared._Lua.AmbientSpaceEffects;
using Content.Shared._Lua.SpaceHazards;
using Content.Shared.Lua.CLVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Map;

namespace Content.Client.Shuttles.UI;

public partial class ShuttleNavControl
{
    private const int MaxNavNebulaContours = 20;
    private const int CelestialContourSegments = 48;

    private readonly AmbientSpaceNebulaVisibility _nebulaVisibility;
    private readonly Dictionary<EntityUid, NavNebulaContourCache> _nebulaNavCache = new();
    private readonly List<(EntityUid Uid, AmbientSpaceFieldComponent Field, TransformComponent Xform, Vector2 Pos, float Radius)> _nebulaFieldScratch = new();
    private readonly Vector2[] _celestialContourScratch = new Vector2[CelestialContourSegments];
    private Vector2[] _nebulaFillScratch = Array.Empty<Vector2>();

    private sealed class NavNebulaContourCache
    {
        public int Seed;
        public float Radius;
        public float Density;
        public Vector2[] Points = Array.Empty<Vector2>();
    }

    private void DrawNebulaContours(
        DrawingHandleScreen handle,
        TransformComponent consoleXform,
        Matrix3x2 worldToShuttle,
        Matrix3x2 shuttleToView)
    {
        var cfg = IoCManager.Resolve<IConfigurationManager>();
        if (cfg.GetCVar(CLVars.AmbientSpaceEffectsQuality) <= 0)
            return;

        var mapId = consoleXform.MapID;
        if (mapId == MapId.Nullspace)
            return;

        var consolePos = _transform.GetWorldPosition(consoleXform);
        var view = worldToShuttle * shuttleToView;
        var maxDist = WorldRange + 64f;
        var cullBox = Box2.CenteredAround(consolePos, new Vector2(maxDist * 2f, maxDist * 2f));
        var drawn = 0;

        _nebulaFieldScratch.Clear();
        var query = EntManager.AllEntityQueryEnumerator<AmbientSpaceFieldComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var field, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            var worldPos = _transform.GetWorldPosition(xform);
            var radius = MathF.Max(field.Radius, 1f);
            if (Vector2.Distance(worldPos, consolePos) > maxDist + radius)
                continue;

            var fieldBounds = _nebulaVisibility.GetPotentialDrawBounds(field, worldPos, consolePos, radius);
            if (!cullBox.Intersects(fieldBounds))
                continue;

            _nebulaFieldScratch.Add((uid, field, xform, worldPos, radius));
        }

        _nebulaFieldScratch.Sort((a, b) =>
        {
            var da = (a.Pos - consolePos).LengthSquared();
            var db = (b.Pos - consolePos).LengthSquared();
            return da.CompareTo(db);
        });

        foreach (var (uid, field, _, worldPos, radius) in _nebulaFieldScratch)
        {
            if (drawn >= MaxNavNebulaContours)
                break;

            if (field.Seed == 0)
                continue;

            var points = GetOrBuildNavContour(uid, field.Seed, radius, field.Density, worldPos);
            if (!_nebulaVisibility.HasVisibleMidLayer(mapId, worldPos, points))
                continue;

            var color = AmbientSpacePalette.ResolveFieldColor(field);
            DrawFilledContour(handle, points, worldPos, view, color.WithAlpha(field.HasWeather ? 0.11f : 0.06f));
            DrawClosedPolyline(handle, points, worldPos, view, color.WithAlpha(0.35f), thickness: 3);
            DrawClosedPolyline(handle, points, worldPos, view, color.WithAlpha(0.9f));
            drawn++;
        }

        PruneNavContourCache();
        DrawCelestialContours(handle, mapId, consolePos, view, maxDist);
    }

    private void DrawCelestialContours(
        DrawingHandleScreen handle,
        MapId mapId,
        Vector2 consolePos,
        Matrix3x2 view,
        float maxDist)
    {
        var query = EntManager.AllEntityQueryEnumerator<SectorCelestialBodyComponent, TransformComponent>();
        while (query.MoveNext(out _, out var body, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            var worldPos = _transform.GetWorldPosition(xform);
            var radius = MathF.Max(body.HazardRadius, body.SpriteRadius);
            if (Vector2.Distance(worldPos, consolePos) > maxDist + radius)
                continue;

            BuildCircleContour(_celestialContourScratch, radius);
            var color = body.Kind == CelestialKind.BlackHole
                ? Color.FromHex("#A040FF").WithAlpha(0.85f)
                : Color.FromHex("#FFB020").WithAlpha(0.85f);
            DrawClosedPolyline(handle, _celestialContourScratch, worldPos, view, color);
        }
    }

    private static void BuildCircleContour(Span<Vector2> points, float radius)
    {
        for (var i = 0; i < points.Length; i++)
        {
            var angle = i * MathF.Tau / points.Length;
            points[i] = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        }
    }

    private Vector2[] GetOrBuildNavContour(EntityUid uid, int seed, float radius, float density, Vector2 worldPos)
    {
        if (!_nebulaNavCache.TryGetValue(uid, out var cache)
            || cache.Seed != seed
            || MathF.Abs(cache.Radius - radius) > 0.01f
            || MathF.Abs(cache.Density - density) > 0.001f
            || cache.Points.Length == 0)
        {
            cache = new NavNebulaContourCache
            {
                Seed = seed,
                Radius = radius,
                Density = density,
                Points = AmbientSpaceNebulaNoise.BuildMidLayerContour(worldPos, radius, seed, density),
            };
            _nebulaNavCache[uid] = cache;
        }

        return cache.Points;
    }

    private void PruneNavContourCache()
    {
        if (_nebulaNavCache.Count <= MaxNavNebulaContours * 2)
            return;

        var toRemove = new List<EntityUid>();
        foreach (var (uid, _) in _nebulaNavCache)
        {
            if (!EntManager.EntityExists(uid))
                toRemove.Add(uid);
        }

        foreach (var uid in toRemove)
            _nebulaNavCache.Remove(uid);
    }

    private static void DrawClosedPolyline(
        DrawingHandleScreen handle,
        ReadOnlySpan<Vector2> worldPoints,
        Vector2 worldOffset,
        Matrix3x2 worldToView,
        Color color,
        int thickness = 1)
    {
        if (worldPoints.Length < 2)
            return;

        var prev = Vector2.Transform(worldPoints[^1] + worldOffset, worldToView);
        foreach (var local in worldPoints)
        {
            var next = Vector2.Transform(local + worldOffset, worldToView);
            if (thickness <= 1)
            {
                handle.DrawLine(prev, next, color);
            }
            else
            {
                var dir = next - prev;
                if (dir.LengthSquared() > 0.0001f)
                {
                    var n = Vector2.Normalize(new Vector2(-dir.Y, dir.X)) * 0.75f;
                    handle.DrawLine(prev + n, next + n, color);
                    handle.DrawLine(prev - n, next - n, color);
                }

                handle.DrawLine(prev, next, color);
            }

            prev = next;
        }
    }

    private void DrawFilledContour(
        DrawingHandleScreen handle,
        ReadOnlySpan<Vector2> worldPoints,
        Vector2 worldOffset,
        Matrix3x2 worldToView,
        Color color)
    {
        if (worldPoints.Length < 3)
            return;

        var count = worldPoints.Length + 2;
        if (_nebulaFillScratch.Length < count)
            _nebulaFillScratch = new Vector2[count];

        _nebulaFillScratch[0] = Vector2.Transform(worldOffset, worldToView);
        for (var i = 0; i < worldPoints.Length; i++)
            _nebulaFillScratch[i + 1] = Vector2.Transform(worldPoints[i] + worldOffset, worldToView);

        _nebulaFillScratch[count - 1] = _nebulaFillScratch[1];
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, new Span<Vector2>(_nebulaFillScratch, 0, count), color);
    }
}
