// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Robust.Shared.Map;

namespace Content.Shared._Lua.AmbientSpaceEffects;

public static class AmbientSpacePalette
{
    public static readonly Color[] Colors =
    [
        Color.FromHex("#5AD0FF"),
        Color.FromHex("#3EE6C7"),
        Color.FromHex("#6DFF8A"),
        Color.FromHex("#F2D45C"),
        Color.FromHex("#FF6B6B"),
        Color.FromHex("#C084FC"),
        Color.FromHex("#FF9F43"),
    ];

    public static int SeedFromChunk(MapId mapId, Vector2i chunk)
    {
        var hash = HashCode.Combine((int) mapId, chunk.X, chunk.Y, 0xA57E11);
        return hash == 0 ? 1 : hash;
    }

    public static bool ShouldSpawnChunk(int seed, float spawnChance)
    {
        var u = unchecked((uint) seed);
        return (u >> 20 & 0xFFFu) / 4095f < spawnChance;
    }

    public static Vector2 OffsetFromSeed(int seed, float span)
    {
        var u = unchecked((uint) seed);
        var nx = (u & 0xFFFFu) / 65535f;
        var ny = (u >> 16 & 0xFFFFu) / 65535f;
        var extent = span * 0.55f;
        return new Vector2((nx - 0.5f) * extent, (ny - 0.5f) * extent);
    }

    public static float RadiusFromSeed(int seed)
    {
        var u = unchecked((uint) seed);
        var roll = (u & 0xFFu) / 255f;
        var jitter = (u >> 8 & 0xFFu) / 255f;
        return roll switch
        {
            < 0.08f => 620f + jitter * 180f,   // 620–800  (~8%)
            < 0.40f => 980f + jitter * 420f,   // 980–1400 (~32%)
            _ => 1500f + jitter * 700f,        // 1500–2200 (~60%)
        };
    }

    public static int ColorIndexFromSeed(int seed)
    {
        if (Colors.Length == 0)
            return 0;

        var u = unchecked((uint) seed);
        return (int) ((u * 1140071485u) >> 27) % Colors.Length;
    }

    public static Color ColorFromSeed(int seed, float? alphaOverride = null)
    {
        if (Colors.Length == 0)
            return Color.White.WithAlpha(alphaOverride ?? 1f);

        return Colors[ColorIndexFromSeed(seed)].WithAlpha(alphaOverride ?? 1f);
    }

    public static Color ResolveFieldColor(AmbientSpaceFieldComponent field)
    {
        if (field.Seed != 0)
            return ColorFromSeed(field.Seed);

        return SnapToPalette(field.Color);
    }

    public static Color SnapToPalette(Color color)
    {
        if (Colors.Length == 0)
            return color;

        var best = Colors[0];
        var bestDist = float.MaxValue;
        foreach (var c in Colors)
        {
            var dr = c.R - color.R;
            var dg = c.G - color.G;
            var db = c.B - color.B;
            var dist = dr * dr + dg * dg + db * db;
            if (dist >= bestDist)
                continue;

            bestDist = dist;
            best = c;
        }

        return best.WithAlpha(color.A);
    }

    public static float ShaderSeedFromField(int seed)
    {
        return (seed & 0xFFFF) / 65535f * 100f;
    }

    public static float DensityFromSeed(int seed)
    {
        var u = unchecked((uint) seed);
        return 0.38f + (u >> 4 & 0xFFu) / 255f * 0.2f;
    }
}
