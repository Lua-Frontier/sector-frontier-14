// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

namespace Content.Server._Lua.SpaceHazards;

public static class SectorCelestialProximity
{
    private const float MinInsideFactor = 0.175f;
    private const float MaxInsideFactor = 1.25f;
    public static float Factor(float distance, float radius)
    {
        var r = MathF.Max(radius, 1f);
        if (distance >= r) return 0f;
        var t = 1f - distance / r;
        return MinInsideFactor + (MaxInsideFactor - MinInsideFactor) * t;
    }
    public static int ScaledHits(int baseMax, float factor)
    {
        if (factor <= 0f) return 0;
        return Math.Max(1, (int) MathF.Ceiling(baseMax * factor));
    }
}
