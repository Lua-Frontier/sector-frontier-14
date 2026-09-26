// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Lua.Shared.AmbientSpaceEffects;

namespace Content.Lua.Shared.SpaceHazards;

public static class NebulaVeilHelpers
{
    public static bool IsInMidZone(
        AmbientSpaceFieldComponent field,
        Vector2 fieldPos,
        Vector2 worldPos,
        float? radiusOverride = null)
    {
        var radius = MathF.Max(radiusOverride ?? field.Radius, 1f);
        var delta = worldPos - fieldPos;
        if (delta.LengthSquared() > radius * radius)
            return false;

        return ContainsPoint(GetOrBuildContour(field, radius), delta);
    }

    public static Vector2[] GetOrBuildContour(AmbientSpaceFieldComponent field, float? radiusOverride = null)
    {
        var radius = MathF.Max(radiusOverride ?? field.Radius, 1f);
        if (field.MidContourCache != null
            && field.ContourCacheSeed == field.Seed
            && MathF.Abs(field.ContourCacheRadius - radius) <= 0.01f
            && MathF.Abs(field.ContourCacheDensity - field.Density) <= 0.001f)
        {
            return field.MidContourCache;
        }

        field.MidContourCache = AmbientSpaceNebulaNoise.BuildMidLayerContour(
            Vector2.Zero,
            radius,
            field.Seed,
            field.Density);
        field.ContourCacheSeed = field.Seed;
        field.ContourCacheRadius = radius;
        field.ContourCacheDensity = field.Density;
        return field.MidContourCache;
    }

    private static bool ContainsPoint(Vector2[] polygon, Vector2 point)
    {
        if (polygon.Length < 3)
            return false;

        var inside = false;
        var previous = polygon.Length - 1;
        for (var current = 0; current < polygon.Length; current++)
        {
            var a = polygon[current];
            var b = polygon[previous];

            if ((a.Y > point.Y) != (b.Y > point.Y) &&
                point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
            {
                inside = !inside;
            }

            previous = current;
        }

        return inside;
    }
}
