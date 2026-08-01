// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Shared._Lua.AmbientSpaceEffects;

namespace Content.Shared._Lua.SpaceHazards;

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

        var contour = AmbientSpaceNebulaNoise.BuildMidLayerContour(fieldPos, radius, field.Seed, field.Density);
        return ContainsPoint(contour, delta);
    }

    private static bool ContainsPoint(ReadOnlySpan<Vector2> polygon, Vector2 point)
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
