// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

namespace Content.Shared._Lua.AmbientSpaceEffects;

public static class AmbientSpaceFieldPvs
{
    public const float InterestMargin = 2048f;

    public static float InterestRange(float radius)
    {
        return MathF.Max(radius, 1f) + InterestMargin;
    }

    public static bool InInterestRange(float distanceSquared, float radius)
    {
        var range = InterestRange(radius);
        return distanceSquared <= range * range;
    }
}
