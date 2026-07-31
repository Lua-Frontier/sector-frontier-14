// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Shared._Lua.AmbientSpaceEffects;
using Content.Shared._Lua.Shuttles.Components;
using Content.Shared._Lua.SpaceHazards;
using Content.Shared._Mono.Radar;
using Content.Shared.Lua.CLVar;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Configuration;
using Robust.Shared.Map;

namespace Content.Client.Shuttles.UI;

public sealed partial class ShuttleMapControl
{
    private const int MaxMapNebulaContourCache = 512;

    private readonly Dictionary<EntityUid, MapNebulaContourCache> _nebulaMapCache = new();
    private readonly List<(EntityUid Uid, AmbientSpaceFieldComponent Field, Vector2 Pos, float Radius)> _nebulaFieldScratch = new();

    private sealed class MapNebulaContourCache
    {
        public int Seed;
        public float Radius;
        public float Density;
        public Vector2[] Points = Array.Empty<Vector2>();
    }

    private void DrawMapSpaceHazards(DrawingHandleScreen handle, Matrix3x2 matty, Box2 viewBox)
    {
        var cfg = IoCManager.Resolve<IConfigurationManager>();
        if (cfg.GetCVar(CLVars.AmbientSpaceEffectsEnabled)
            && cfg.GetCVar(CLVars.AmbientSpaceEffectsQuality) > 0)
        {
            DrawMapNebulaContours(handle, matty, viewBox);
        }

        DrawMapCelestialIcons(handle, matty, viewBox);
        DrawMapDangerousNebulaIcons(handle, matty, viewBox);
    }

    private void DrawMapNebulaContours(DrawingHandleScreen handle, Matrix3x2 matty, Box2 viewBox)
    {
        var mapId = ViewingMap;
        if (mapId == MapId.Nullspace)
            return;

        _nebulaFieldScratch.Clear();
        var query = EntManager.AllEntityQueryEnumerator<AmbientSpaceFieldComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var field, out var xform))
        {
            if (xform.MapID != mapId || field.Seed == 0)
                continue;

            var worldPos = _xformSystem.GetWorldPosition(xform);
            var radius = MathF.Max(field.Radius, 1f);
            var fieldBox = Box2.CenteredAround(worldPos, new Vector2(radius * 2f, radius * 2f));
            if (!viewBox.Intersects(fieldBox))
                continue;

            _nebulaFieldScratch.Add((uid, field, worldPos, radius));
        }

        foreach (var (uid, field, worldPos, radius) in _nebulaFieldScratch)
        {
            var points = GetOrBuildMapContour(uid, field.Seed, radius, field.Density, worldPos);
            var color = AmbientSpacePalette.ResolveFieldColor(field).WithAlpha(0.2f);
            DrawMapClosedPolyline(handle, points, worldPos, matty, color);
        }

        PruneMapContourCache();
    }

    private void DrawMapCelestialIcons(DrawingHandleScreen handle, Matrix3x2 matty, Box2 viewBox)
    {
        var mapId = ViewingMap;
        if (mapId == MapId.Nullspace)
            return;

        var cache = IoCManager.Resolve<IResourceCache>();
        var eyePos = Offset;
        const float iconBase = 18f;

        var query = EntManager.AllEntityQueryEnumerator<SectorCelestialBodyComponent, RadarBlipIconComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var body, out var icon, out var xform))
        {
            if (xform.MapID != mapId || icon.Icon == default)
                continue;

            var worldPos = _xformSystem.GetWorldPosition(xform);
            var maxRadius = GetCelestialMapRadius(body);
            if (!viewBox.Enlarged(maxRadius + 64f).Contains(worldPos))
                continue;

            DrawMapCelestialRadii(handle, matty, worldPos, body);
            DrawMapHazardIcon(handle, cache, matty, uid, icon, worldPos, eyePos, iconBase);
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

    private void DrawMapCelestialRadii(DrawingHandleScreen handle, Matrix3x2 matty, Vector2 worldPos, SectorCelestialBodyComponent body)
    {
        var adjusted = Vector2.Transform(worldPos, matty);
        var localPos = ScalePosition(adjusted with { Y = -adjusted.Y });

        void Ring(float radius, Color color)
        {
            if (radius <= 1f)
                return;

            handle.DrawCircle(localPos, radius * MinimapScale, color, filled: false);
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

    private void DrawMapDangerousNebulaIcons(DrawingHandleScreen handle, Matrix3x2 matty, Box2 viewBox)
    {
        var mapId = ViewingMap;
        if (mapId == MapId.Nullspace)
            return;

        var cache = IoCManager.Resolve<IResourceCache>();
        var eyePos = Offset;
        const float iconBase = 16f;

        var query = EntManager.AllEntityQueryEnumerator<AmbientSpaceFieldComponent, RadarBlipIconComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var field, out var icon, out var xform))
        {
            if (xform.MapID != mapId || field.Weather == null || icon.Icon == default)
                continue;

            var worldPos = _xformSystem.GetWorldPosition(xform);
            if (!viewBox.Enlarged(64f).Contains(worldPos))
                continue;

            DrawMapHazardIcon(handle, cache, matty, uid, icon, worldPos, eyePos, iconBase);
        }
    }

    private void DrawMapHazardIcon(
        DrawingHandleScreen handle,
        IResourceCache cache,
        Matrix3x2 matty,
        EntityUid uid,
        RadarBlipIconComponent icon,
        Vector2 worldPos,
        Vector2 eyePos,
        float iconBase)
    {
        var worldDist = Vector2.Distance(worldPos, eyePos);

        if (!cache.TryGetResource<TextureResource>(icon.Icon, out var texRes))
            return;

        var adjusted = Vector2.Transform(worldPos, matty);
        var localPos = ScalePosition(adjusted with { Y = -adjusted.Y });

        var s = iconBase * UIScale * icon.Scale;
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
            handle.DrawTextureRect(texRes.Texture, new UIBox2(leftCentre - half, leftCentre + half));
            handle.DrawTextureRect(secondaryTex.Texture, new UIBox2(rightCentre - half, rightCentre + half));
        }
        else
        {
            handle.DrawTextureRect(texRes.Texture, new UIBox2(localPos - half, localPos + half));
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

        var labelDimensions = handle.GetDimensions(_font, labelText, 1f);
        var labelPos = localPos + new Vector2(s * 0.6f, -labelDimensions.Y / 2f);

        var labelColor = Color.White;
        if (EntManager.TryGetComponent(uid, out RadarBlipComponent? blip))
            labelColor = blip.RadarColor;

        handle.DrawString(_font, labelPos, labelText, labelColor);
    }

    private Vector2[] GetOrBuildMapContour(EntityUid uid, int seed, float radius, float density, Vector2 worldPos)
    {
        if (!_nebulaMapCache.TryGetValue(uid, out var cache)
            || cache.Seed != seed
            || MathF.Abs(cache.Radius - radius) > 0.01f
            || MathF.Abs(cache.Density - density) > 0.001f
            || cache.Points.Length == 0)
        {
            cache = new MapNebulaContourCache
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

    private void PruneMapContourCache()
    {
        if (_nebulaMapCache.Count <= MaxMapNebulaContourCache)
            return;

        var toRemove = new List<EntityUid>();
        foreach (var (uid, _) in _nebulaMapCache)
        {
            if (!EntManager.EntityExists(uid))
                toRemove.Add(uid);
        }

        foreach (var uid in toRemove)
            _nebulaMapCache.Remove(uid);
    }

    private void DrawMapClosedPolyline(
        DrawingHandleScreen handle,
        ReadOnlySpan<Vector2> worldPoints,
        Vector2 worldOffset,
        Matrix3x2 matty,
        Color color)
    {
        if (worldPoints.Length < 2)
            return;

        Vector2 ToUi(Vector2 local)
        {
            var world = local + worldOffset;
            var adjusted = Vector2.Transform(world, matty);
            return ScalePosition(adjusted with { Y = -adjusted.Y });
        }

        var prev = ToUi(worldPoints[^1]);
        foreach (var local in worldPoints)
        {
            var next = ToUi(local);
            handle.DrawLine(prev, next, color);
            prev = next;
        }
    }

    private void DrawDroneRoutes(DrawingHandleScreen handle, Matrix3x2 matty, float animOffset)
    {
        if (_droneRoutes == null || _droneRoutes.Count == 0)
            return;

        var color = Color.Cyan.WithAlpha(0.85f);
        foreach (var route in _droneRoutes)
        {
            if (route.Points.Count < 2)
                continue;

            Vector2? prev = null;
            foreach (var netCoords in route.Points)
            {
                var coords = EntManager.GetCoordinates(netCoords);
                var mapCoords = _xformSystem.ToMapCoordinates(coords);
                if (mapCoords.MapId != ViewingMap)
                {
                    prev = null;
                    continue;
                }

                var adjusted = Vector2.Transform(mapCoords.Position, matty);
                var ui = ScalePosition(adjusted with { Y = -adjusted.Y });
                if (prev != null)
                    handle.DrawDottedLine(prev.Value, ui, color, animOffset);
                prev = ui;
            }
        }
    }
}
