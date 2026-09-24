// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Lua.Shared.AmbientSpaceEffects;
using Content.Lua.Shared.Shuttles.Components;
using Content.Lua.Shared.SpaceHazards;
using Content.Lua.UIKit.Shuttles;
using Content.Lua.UIKit.Styles;
using Content.Lua.Common.CLVar;
using Content.Shared._Mono.Radar;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Lua.Client.Shuttles;

public sealed partial class ShuttleRadarLuaDraw
{
    public void DrawNavOverlays(in ShuttleNavLuaDrawContext ctx)
    {
        DrawNebulaContours(ctx);
        DrawSpaceHazardRadarIcons(ctx);
        DrawDroneRoutes(ctx);
    }

    private void DrawNebulaContours(in ShuttleNavLuaDrawContext ctx)
    {
        var cfg = IoCManager.Resolve<IConfigurationManager>();
        if (cfg.GetCVar(CLVars.AmbientSpaceEffectsQuality) <= 0)
            return;

        var mapId = ctx.ConsoleXform.MapID;
        if (mapId == MapId.Nullspace)
            return;

        var consolePos = ctx.Transform.GetWorldPosition(ctx.ConsoleXform);
        var view = ctx.WorldToShuttle * ctx.ShuttleToView;
        var maxDist = ctx.WorldRange + 64f;
        var cullBox = Box2.CenteredAround(consolePos, new Vector2(maxDist * 2f, maxDist * 2f));
        var drawn = 0;

        _nebulaFieldScratch.Clear();
        var query = ctx.EntManager.AllEntityQueryEnumerator<AmbientSpaceFieldComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var field, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            var worldPos = ctx.Transform.GetWorldPosition(xform);
            var radius = MathF.Max(field.Radius, 1f);
            if (Vector2.Distance(worldPos, consolePos) > maxDist + radius)
                continue;

            var fieldBounds = NebulaVisibility.GetPotentialDrawBounds(field, worldPos, consolePos, radius);
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
            if (!NebulaVisibility.HasVisibleMidLayer(mapId, worldPos, points))
                continue;

            var color = AmbientSpacePalette.ResolveFieldColor(field);
            DrawFilledContour(ctx.Handle, points, worldPos, view, color.WithAlpha(field.HasWeather ? 0.11f : 0.06f));
            DrawClosedPolyline(ctx.Handle, points, worldPos, view, color.WithAlpha(0.35f), thickness: 3);
            DrawClosedPolyline(ctx.Handle, points, worldPos, view, color.WithAlpha(0.9f));
            drawn++;
        }

        PruneNavContourCache(ctx.EntManager);
        DrawCelestialContours(ctx, mapId, consolePos, view, maxDist);
    }

    private void DrawCelestialContours(
        in ShuttleNavLuaDrawContext ctx,
        MapId mapId,
        Vector2 consolePos,
        Matrix3x2 view,
        float maxDist)
    {
        var query = ctx.EntManager.AllEntityQueryEnumerator<SectorCelestialBodyComponent, TransformComponent>();
        while (query.MoveNext(out _, out var body, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            var worldPos = ctx.Transform.GetWorldPosition(xform);
            var radius = MathF.Max(body.HazardRadius, body.SpriteRadius);
            if (Vector2.Distance(worldPos, consolePos) > maxDist + radius)
                continue;

            BuildCircleContour(_celestialContourScratch, radius);
            var color = body.Kind == CelestialKind.BlackHole
                ? Color.FromHex("#A040FF").WithAlpha(0.85f)
                : Color.FromHex("#FFB020").WithAlpha(0.85f);
            DrawClosedPolyline(ctx.Handle, _celestialContourScratch, worldPos, view, color);
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
            cache = new NebulaContourCache
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

    private void PruneNavContourCache(IEntityManager entManager)
    {
        if (_nebulaNavCache.Count <= MaxNavNebulaContours * 2)
            return;

        var toRemove = new List<EntityUid>();
        foreach (var (uid, _) in _nebulaNavCache)
        {
            if (!entManager.EntityExists(uid))
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
                LunaDraw.Line(handle, prev, next, color);
            }
            else
            {
                var dir = next - prev;
                if (dir.LengthSquared() > 0.0001f)
                {
                    var n = Vector2.Normalize(new Vector2(-dir.Y, dir.X)) * 0.75f;
                    LunaDraw.Line(handle, prev + n, next + n, color);
                    LunaDraw.Line(handle, prev - n, next - n, color);
                }

                LunaDraw.Line(handle, prev, next, color);
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

    private void DrawSpaceHazardRadarIcons(in ShuttleNavLuaDrawContext ctx)
    {
        var mapId = ctx.ConsoleXform.MapID;
        if (mapId == MapId.Nullspace)
            return;

        var cache = IoCManager.Resolve<IResourceCache>();
        var view = ctx.WorldToShuttle * ctx.ShuttleToView;
        var uiXCentre = (int) ctx.Width / 2;
        var uiYCentre = (int) ctx.Height / 2;

        const float fullScaleDistance = 200f;
        const float minDistanceScale = 0.35f;
        const float scaleEndDistance = 800f;
        var maxScaleRange = scaleEndDistance;

        var celestialQuery = ctx.EntManager.AllEntityQueryEnumerator<SectorCelestialBodyComponent, RadarBlipIconComponent, TransformComponent>();
        while (celestialQuery.MoveNext(out var uid, out _, out var icon, out var xform))
            TryDrawHazardIcon(ctx, cache, uid, icon, xform, mapId, view, uiXCentre, uiYCentre, fullScaleDistance, minDistanceScale, maxScaleRange);

        var fieldQuery = ctx.EntManager.AllEntityQueryEnumerator<AmbientSpaceFieldComponent, RadarBlipIconComponent, TransformComponent>();
        while (fieldQuery.MoveNext(out var uid, out var field, out var icon, out var xform))
        {
            if (!field.HasWeather)
                continue;

            TryDrawHazardIcon(ctx, cache, uid, icon, xform, mapId, view, uiXCentre, uiYCentre, fullScaleDistance, minDistanceScale, maxScaleRange);
        }
    }

    private void TryDrawHazardIcon(
        in ShuttleNavLuaDrawContext ctx,
        IResourceCache cache,
        EntityUid uid,
        RadarBlipIconComponent icon,
        TransformComponent xform,
        MapId mapId,
        Matrix3x2 view,
        int uiXCentre,
        int uiYCentre,
        float fullScaleDistance,
        float minDistanceScale,
        float maxScaleRange)
    {
        if (xform.MapID != mapId || icon.Icon == default)
            return;

        var worldPos = ctx.Transform.GetWorldPosition(xform);
        var worldDist = Vector2.Distance(worldPos, ctx.ConsoleMapPos);
        if (icon.MaxDistance > 0f && worldDist > icon.MaxDistance)
            return;

        if (!cache.TryGetResource<TextureResource>(icon.Icon, out var texRes))
            return;

        var uiPosition = Vector2.Transform(worldPos, view) / ctx.UIScale;
        var uiXOffset = uiPosition.X - uiXCentre;
        var uiYOffset = uiPosition.Y - uiYCentre;
        var uiDistance = (int) Math.Sqrt(Math.Pow(uiXOffset, 2) + Math.Pow(uiYOffset, 2));
        if (uiDistance > 0)
        {
            var uiX = uiXCentre * uiXOffset / uiDistance;
            var uiY = uiYCentre * uiYOffset / uiDistance;
            var isOutsideRadarCircle = uiDistance > Math.Abs(uiX) && uiDistance > Math.Abs(uiY);
            if (isOutsideRadarCircle)
            {
                uiX = uiXCentre * uiXOffset / uiDistance * 0.95f;
                uiY = uiYCentre * uiYOffset / uiDistance * 0.95f;
                uiPosition = new Vector2(uiX + uiXCentre, uiY + uiYCentre);
            }
        }

        var isHovered = Vector2.Distance(ctx.ScaledMouseUiPos, uiPosition * ctx.UIScale) < 30f;
        var distanceScale = isHovered || worldDist <= fullScaleDistance
            ? 1f
            : MathF.Max(minDistanceScale, 1f - (worldDist - fullScaleDistance) / (maxScaleRange - fullScaleDistance) * (1f - minDistanceScale));

        var s = (ctx.RadarBlipSize * ctx.UIScale) * icon.Scale * distanceScale;
        var half = new Vector2(s / 2f, s / 2f);
        var centre = uiPosition * ctx.UIScale;

        TextureResource? secondaryTex = null;
        var hasSecondary = icon.SecondaryIcon is { } sec
                           && sec != default
                           && sec != icon.Icon
                           && cache.TryGetResource(sec, out secondaryTex);

        if (hasSecondary && secondaryTex != null)
        {
            var gap = s * 0.12f;
            var leftCentre = centre - new Vector2(half.X + gap * 0.5f, 0f);
            var rightCentre = centre + new Vector2(half.X + gap * 0.5f, 0f);
            ctx.Handle.DrawTextureRect(texRes.Texture, new UIBox2(leftCentre - half, leftCentre + half));
            ctx.Handle.DrawTextureRect(secondaryTex.Texture, new UIBox2(rightCentre - half, rightCentre + half));
        }
        else
        {
            ctx.Handle.DrawTextureRect(texRes.Texture, new UIBox2(centre - half, centre + half));
        }

        if (icon.Label is not { } labelLoc || string.IsNullOrEmpty(labelLoc))
            return;

        var labelName = Loc.GetString(labelLoc);
        var displayedDistance = worldDist < 50f ? $"{worldDist:0.0}" : worldDist < 1000 ? $"{worldDist:0}" : $"{worldDist / 1000:0.0}k";
        var labelText = Loc.GetString("shuttle-console-iff-label", ("name", labelName), ("distance", displayedDistance));

        const float dimScale = 0.9f;
        var labelFontScale = QuantizeRadarLabelScale(dimScale * distanceScale);
        var textScale = ctx.UIScale * labelFontScale;
        var labelDimensions = ctx.Handle.GetDimensions(ctx.Font, labelText, labelFontScale);
        var blipSize = ctx.RadarBlipSize * 0.7f * distanceScale;
        var labelOffset = new Vector2
        {
            X = uiPosition.X > ctx.Width / 2f
                ? -labelDimensions.X - blipSize
                : blipSize,
            Y = -labelDimensions.Y / 2f
        };

        var labelColor = Color.White;
        if (ctx.EntManager.TryGetComponent(uid, out RadarBlipComponent? blip))
            labelColor = isHovered ? blip.HighlightedRadarColor : blip.RadarColor;

        ctx.Handle.DrawString(ctx.Font, (uiPosition + labelOffset) * ctx.UIScale, labelText, textScale, labelColor);
    }

    private static float QuantizeRadarLabelScale(float scale)
    {
        if (scale >= 0.85f)
            return 0.9f;
        if (scale >= 0.70f)
            return 0.75f;
        if (scale >= 0.55f)
            return 0.6f;
        if (scale >= 0.42f)
            return 0.45f;
        return 0.35f;
    }

    private void DrawDroneRoutes(in ShuttleNavLuaDrawContext ctx)
    {
        if (ctx.DroneRoutes == null || ctx.DroneRoutes.Count == 0)
            return;

        var timing = IoCManager.Resolve<IGameTiming>();
        var animOffset = (float) timing.RealTime.TotalSeconds * 30f;
        var color = Color.Cyan.WithAlpha(0.7f);
        var worldToView = ctx.WorldToShuttle * ctx.ShuttleToView;

        foreach (var route in ctx.DroneRoutes)
        {
            if (ctx.DroneRouteFilter != null && !ctx.DroneRouteFilter.Contains(route.Steerer))
                continue;

            if (ctx.DroneRouteFilter is { Count: 0 })
                continue;

            if (route.Points.Count < 2)
                continue;

            Vector2? prev = null;
            foreach (var netCoords in route.Points)
            {
                var coords = ctx.EntManager.GetCoordinates(netCoords);
                var mapCoords = ctx.Transform.ToMapCoordinates(coords);
                if (mapCoords.MapId == MapId.Nullspace)
                {
                    prev = null;
                    continue;
                }

                var ui = Vector2.Transform(mapCoords.Position, worldToView);
                if (prev != null)
                    LunaDraw.DashedLine(ctx.Handle, prev.Value, ui, color, dashLength: 6f, gapLength: 3f, offset: animOffset);
                prev = ui;
            }
        }
    }
}
