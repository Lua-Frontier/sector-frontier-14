// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
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

        var p = delta / radius;
        return AmbientSpaceNebulaNoise.SamplePresence(p, field.Seed, field.Density, 1f) > 0.12f;
    }
}
