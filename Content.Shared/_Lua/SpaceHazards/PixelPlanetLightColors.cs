// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

namespace Content.Shared._Lua.SpaceHazards;

public static class PixelPlanetLightColors
{
    public const int Count = 9;
    public static readonly Color PlanetSunlight = Color.White;
    private static readonly Color[] Star =
    [
        C(0.467f, 0.839f, 0.757f),
        C(1.000f, 0.600f, 0.200f),
        C(0.560f, 0.800f, 1.000f),
        C(0.520f, 0.920f, 0.200f),
        C(0.680f, 0.680f, 0.740f),
        C(0.820f, 0.780f, 0.620f),
        C(0.720f, 0.840f, 1.000f),
        C(1.000f, 0.680f, 0.280f),
        C(0.120f, 0.720f, 0.820f),
    ];
    private static readonly Color[] BlackHoleRing =
    [
        C(1.000f, 0.961f, 0.251f),
        C(1.000f, 0.700f, 0.300f),
        C(0.620f, 0.840f, 1.000f),
        C(0.720f, 1.000f, 0.200f),
        C(0.760f, 0.760f, 0.820f),
        C(0.820f, 0.620f, 0.320f),
        C(0.560f, 0.760f, 1.000f),
        C(1.000f, 0.720f, 0.320f),
        C(0.220f, 0.680f, 0.860f),
    ];

    public static Color StarGlow(byte palette) => Star[Clamp(palette)];

    public static Color BlackHoleGlow(byte palette) => BlackHoleRing[Clamp(palette)];

    private static int Clamp(byte palette) => Math.Clamp((int)palette, 0, Count - 1);

    private static Color C(float r, float g, float b) => new(r, g, b);
}
