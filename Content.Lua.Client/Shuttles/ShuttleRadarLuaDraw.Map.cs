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

namespace Content.Lua.Client.Shuttles;

public sealed partial class ShuttleRadarLuaDraw
{
    public void DrawMapOverlays(in ShuttleMapLuaDrawContext ctx)
    {
        var cfg = IoCManager.Resolve<IConfigurationManager>();
        if (cfg.GetCVar(CLVars.AmbientSpaceEffectsQuality) > 0)
            DrawMapNebulaContours(ctx);

        DrawMapCelestialIcons(ctx);
        DrawMapDangerousNebulaIcons(ctx);
    }

    public void DrawMapDroneRoutes(in ShuttleMapLuaDroneContext ctx)
    {
        if (ctx.DroneRoutes == null || ctx.DroneRoutes.Count == 0)
            return;

        var color = Color.Cyan.WithAlpha(0.85f);
        foreach (var route in ctx.DroneRoutes)
        {
            if (route.Points.Count < 2)
                continue;

            Vector2? prev = null;
            foreach (var netCoords in route.Points)
            {
                var coords = ctx.EntManager.GetCoordinates(netCoords);
                var mapCoords = ctx.Transform.ToMapCoordinates(coords);
                if (mapCoords.MapId != ctx.ViewingMap)
                {
                    prev = null;
                    continue;
                }

                var adjusted = Vector2.Transform(mapCoords.Position, ctx.MapTransform);
                var ui = ctx.ScalePosition(adjusted with { Y = -adjusted.Y });
                if (prev != null)
                    LunaDraw.DashedLine(ctx.Handle, prev.Value, ui, color, dashLength: 8f, gapLength: 2f, offset: ctx.AnimOffset);
                prev = ui;
            }
        }
    }

    private void DrawMapNebulaContours(in ShuttleMapLuaDrawContext ctx)
    {
        if (ctx.ViewingMap == MapId.Nullspace)
            return;

        _mapFieldScratch.Clear();
        var query = ctx.EntManager.AllEntityQueryEnumerator<AmbientSpaceFieldComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var field, out var xform))
        {
            if (xform.MapID != ctx.ViewingMap || field.Seed == 0)
                continue;

            var worldPos = ctx.Transform.GetWorldPosition(xform);
            var radius = MathF.Max(field.Radius, 1f);
            var fieldBox = Box2.CenteredAround(worldPos, new Vector2(radius * 2f, radius * 2f));
            if (!ctx.ViewBox.Intersects(fieldBox))
                continue;

            _mapFieldScratch.Add((uid, field, worldPos, radius));
        }

        foreach (var (uid, field, worldPos, radius) in _mapFieldScratch)
        {
            var points = GetOrBuildMapContour(uid, field.Seed, radius, field.Density, worldPos);
            var color = AmbientSpacePalette.ResolveFieldColor(field);
            DrawMapFilledContour(ctx, points, worldPos, color.WithAlpha(field.HasWeather ? 0.1f : 0.05f));
            DrawMapClosedPolyline(ctx, points, worldPos, color.WithAlpha(0.85f));
        }

        PruneMapContourCache(ctx.EntManager);
    }

    private void DrawMapCelestialIcons(in ShuttleMapLuaDrawContext ctx)
    {
        if (ctx.ViewingMap == MapId.Nullspace)
            return;

        var cache = IoCManager.Resolve<IResourceCache>();
        const float iconBase = 18f;

        var query = ctx.EntManager.AllEntityQueryEnumerator<SectorCelestialBodyComponent, RadarBlipIconComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var body, out var icon, out var xform))
        {
            if (xform.MapID != ctx.ViewingMap || icon.Icon == default)
                continue;

            var worldPos = ctx.Transform.GetWorldPosition(xform);
            var maxRadius = GetCelestialMapRadius(body);
            if (!ctx.ViewBox.Enlarged(maxRadius + 64f).Contains(worldPos))
                continue;

            DrawMapCelestialRadii(ctx, worldPos, body);
            DrawMapHazardIcon(ctx, cache, uid, icon, worldPos, iconBase);
        }
    }

    private static float GetCelestialMapRadius(SectorCelestialBodyComponent body)
    {
        return body.Kind switch
        {
            CelestialKind.BlackHole => MathF.Max(body.PullRadius, MathF.Max(body.RadiationRange, body.HazardRadius)),
            _ => MathF.Max(body.RadiationRange, body.HazardRadius),
        };
    }

    private static void DrawMapCelestialRadii(in ShuttleMapLuaDrawContext ctx, Vector2 worldPos, SectorCelestialBodyComponent body)
    {
        var handle = ctx.Handle;
        var minimapScale = ctx.MinimapScale;
        var adjusted = Vector2.Transform(worldPos, ctx.MapTransform);
        var localPos = ctx.ScalePosition(adjusted with { Y = -adjusted.Y });

        void Ring(float radius, Color color)
        {
            if (radius <= 1f)
                return;

            LunaDraw.Circle(handle, localPos, radius * minimapScale, color, filled: false);
        }

        if (body.Kind == CelestialKind.BlackHole)
        {
            Ring(body.PullRadius, Color.FromHex("#A040FF").WithAlpha(0.35f));
            Ring(body.HazardRadius, Color.FromHex("#D080FF").WithAlpha(0.45f));
            Ring(body.EventHorizonRadius, Color.FromHex("#F5F540").WithAlpha(0.55f));
        }
        else
        {
            Ring(body.RadiationRange, Color.FromHex("#FFE080").WithAlpha(0.3f));
            Ring(body.HazardRadius, Color.FromHex("#FFB020").WithAlpha(0.45f));
        }
    }

    private void DrawMapDangerousNebulaIcons(in ShuttleMapLuaDrawContext ctx)
    {
        if (ctx.ViewingMap == MapId.Nullspace)
            return;

        var cache = IoCManager.Resolve<IResourceCache>();
        const float iconBase = 16f;

        var query = ctx.EntManager.AllEntityQueryEnumerator<AmbientSpaceFieldComponent, RadarBlipIconComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var field, out var icon, out var xform))
        {
            if (xform.MapID != ctx.ViewingMap || !field.HasWeather || icon.Icon == default)
                continue;

            var worldPos = ctx.Transform.GetWorldPosition(xform);
            if (!ctx.ViewBox.Enlarged(64f).Contains(worldPos))
                continue;

            DrawMapHazardIcon(ctx, cache, uid, icon, worldPos, iconBase);
        }
    }

    private static void DrawMapHazardIcon(
        in ShuttleMapLuaDrawContext ctx,
        IResourceCache cache,
        EntityUid uid,
        RadarBlipIconComponent icon,
        Vector2 worldPos,
        float iconBase)
    {
        var worldDist = Vector2.Distance(worldPos, ctx.EyeOffset);

        if (!cache.TryGetResource<TextureResource>(icon.Icon, out var texRes))
            return;

        var adjusted = Vector2.Transform(worldPos, ctx.MapTransform);
        var localPos = ctx.ScalePosition(adjusted with { Y = -adjusted.Y });

        var s = iconBase * ctx.UIScale * icon.Scale;
        var half = new Vector2(s / 2f, s / 2f);

        TextureResource? secondaryTex = null;
        var hasSecondary = icon.SecondaryIcon is { } sec
                           && sec != default
                           && sec != icon.Icon
                           && cache.TryGetResource(sec, out secondaryTex);

        if (hasSecondary && secondaryTex != null)
        {
            var gap = s * 0.12f;
            var leftCentre = localPos - new Vector2(half.X + gap * 0.5f, 0f);
            var rightCentre = localPos + new Vector2(half.X + gap * 0.5f, 0f);
            ctx.Handle.DrawTextureRect(texRes.Texture, new UIBox2(leftCentre - half, leftCentre + half));
            ctx.Handle.DrawTextureRect(secondaryTex.Texture, new UIBox2(rightCentre - half, rightCentre + half));
        }
        else
        {
            ctx.Handle.DrawTextureRect(texRes.Texture, new UIBox2(localPos - half, localPos + half));
        }

        if (icon.Label is not { } labelLoc || string.IsNullOrEmpty(labelLoc))
            return;

        var labelName = Loc.GetString(labelLoc);
        var displayedDistance = worldDist < 50f
            ? $"{worldDist:0.0}"
            : worldDist < 1000
                ? $"{worldDist:0}"
                : $"{worldDist / 1000:0.0}k";
        var labelText = Loc.GetString("shuttle-console-iff-label", ("name", labelName), ("distance", displayedDistance));

        var labelDimensions = ctx.Handle.GetDimensions(ctx.Font, labelText, 1f);
        var labelPos = localPos + new Vector2(s * 0.6f, -labelDimensions.Y / 2f);

        var labelColor = Color.White;
        if (ctx.EntManager.TryGetComponent(uid, out RadarBlipComponent? blip))
            labelColor = blip.RadarColor;

        ctx.DrawSoftText(ctx.Handle, labelText, labelPos, 1f, labelColor.WithAlpha(0.9f), Color.Black.WithAlpha(0.5f));
    }

    private Vector2[] GetOrBuildMapContour(EntityUid uid, int seed, float radius, float density, Vector2 worldPos)
    {
        if (!_nebulaMapCache.TryGetValue(uid, out var cache)
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
            _nebulaMapCache[uid] = cache;
        }

        return cache.Points;
    }

    private void DrawMapFilledContour(
        in ShuttleMapLuaDrawContext ctx,
        ReadOnlySpan<Vector2> worldPoints,
        Vector2 worldOffset,
        Color color)
    {
        if (worldPoints.Length < 3)
            return;

        var count = worldPoints.Length + 2;
        if (_mapNebulaFillScratch.Length < count)
            _mapNebulaFillScratch = new Vector2[count];

        var center = Vector2.Transform(worldOffset, ctx.MapTransform);
        _mapNebulaFillScratch[0] = ctx.ScalePosition(center with { Y = -center.Y });
        for (var i = 0; i < worldPoints.Length; i++)
        {
            var point = Vector2.Transform(worldPoints[i] + worldOffset, ctx.MapTransform);
            _mapNebulaFillScratch[i + 1] = ctx.ScalePosition(point with { Y = -point.Y });
        }

        _mapNebulaFillScratch[count - 1] = _mapNebulaFillScratch[1];
        ctx.Handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, new Span<Vector2>(_mapNebulaFillScratch, 0, count), color);
    }

    private void PruneMapContourCache(IEntityManager entManager)
    {
        if (_nebulaMapCache.Count <= MaxMapNebulaContourCache)
            return;

        var toRemove = new List<EntityUid>();
        foreach (var (uid, _) in _nebulaMapCache)
        {
            if (!entManager.EntityExists(uid))
                toRemove.Add(uid);
        }

        foreach (var uid in toRemove)
            _nebulaMapCache.Remove(uid);
    }

    private static void DrawMapClosedPolyline(
        in ShuttleMapLuaDrawContext ctx,
        ReadOnlySpan<Vector2> worldPoints,
        Vector2 worldOffset,
        Color color)
    {
        if (worldPoints.Length < 2)
            return;

        var mapTransform = ctx.MapTransform;
        var scalePosition = ctx.ScalePosition;
        var handle = ctx.Handle;

        Vector2 ToUi(Vector2 local)
        {
            var world = local + worldOffset;
            var adjusted = Vector2.Transform(world, mapTransform);
            return scalePosition(adjusted with { Y = -adjusted.Y });
        }

        var prev = ToUi(worldPoints[^1]);
        foreach (var local in worldPoints)
        {
            var next = ToUi(local);
            LunaDraw.Line(handle, prev, next, color);
            prev = next;
        }
    }
}
