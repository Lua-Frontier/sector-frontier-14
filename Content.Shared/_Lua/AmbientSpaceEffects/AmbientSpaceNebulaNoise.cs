// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using System.Numerics;

namespace Content.Shared._Lua.AmbientSpaceEffects;

public static class AmbientSpaceNebulaNoise
{
    public const int ContourSegments = 32;
    public const int NavContourSegments = 24;
    public const int NavContourSteps = 12;
    /// <summary>Noise was tuned at this world radius; scale UV frequency so larger fields stay filled.</summary>
    public const float NoiseReferenceRadius = 500f;
    private const float Quality = 1f;

    public static float Hash21(Vector2 p)
    {
        var dot = p.X * 127.1f + p.Y * 311.7f;
        return Fract(MathF.Sin(dot) * 43758.5453f);
    }

    public static float VNoise(Vector2 p)
    {
        var i = new Vector2(MathF.Floor(p.X), MathF.Floor(p.Y));
        var f = new Vector2(p.X - i.X, p.Y - i.Y);
        var a = Hash21(i);
        var b = Hash21(i + new Vector2(1f, 0f));
        var c = Hash21(i + new Vector2(0f, 1f));
        var d = Hash21(i + new Vector2(1f, 1f));
        var u = new Vector2(f.X * f.X * (3f - 2f * f.X), f.Y * f.Y * (3f - 2f * f.Y));
        return Lerp(Lerp(a, b, u.X), Lerp(c, d, u.X), u.Y);
    }

    public static float Fbm(Vector2 p)
    {
        var v = 0f;
        var a = 0.5f;
        var pt = p;
        for (var i = 0; i < 5; i++)
        {
            v += a * VNoise(pt);
            pt = pt * 2.02f + new Vector2(17f, 9f);
            a *= 0.5f;
        }

        return v;
    }

    public static float WarpedFbm(Vector2 p)
    {
        var q = new Vector2(Fbm(p), Fbm(p + new Vector2(5.2f, 1.3f)));
        var r = new Vector2(
            Fbm(p + 1.5f * q + new Vector2(1.7f, 9.2f)),
            Fbm(p + 1.5f * q + new Vector2(8.3f, 2.8f)));
        return Fbm(p + 2f * r);
    }

    public static float SamplePresence(Vector2 p, int seed, float density, float layerId, float worldRadius = NoiseReferenceRadius)
    {
        var scale = Lerp(1.55f, 2.05f, Quality) * 1f;
        var layerBias = layerId * 2.3f;
        var seedF = AmbientSpacePalette.ShaderSeedFromField(seed);
        var sizeScale = MathF.Max(worldRadius, 1f) / NoiseReferenceRadius;

        var baseP = p * sizeScale * new Vector2(scale * 1.35f, scale * 0.95f);
        baseP += new Vector2(seedF * 0.13f + layerBias, seedF * 0.07f);

        var n = WarpedFbm(baseP);
        var detail = Fbm(baseP * 2.6f + new Vector2(3.1f, 5.7f));
        var ridges = Fbm(baseP * 0.9f + new Vector2(9f, 2f));

        var ap = new Vector2(
            MathF.Abs(baseP.X * 1.1f + ridges * 0.4f),
            MathF.Abs(baseP.Y * 1.1f + detail * 0.35f));
        var ang = MathF.Max(ap.X, ap.Y);
        var angFold = Fract(ang * 0.85f + n * 0.2f);
        var corner = Lerp(0.82f, 1.12f, Step(0.55f, angFold) * Step(angFold, 0.78f));

        var radial = new Vector2(p.X * 1.02f, p.Y * 0.95f).Length();
        var edgeWobble = (detail - 0.5f) * 0.18f + (angFold - 0.5f) * 0.08f + (ridges - 0.5f) * 0.12f;
        var outer = 1f - Smoothstep(0.78f + edgeWobble, 1.02f + edgeWobble * 0.25f, radial);

        var dens = Math.Clamp(density, 0.25f, 0.85f);
        var body = Smoothstep(0.34f - dens * 0.1f, 0.6f + dens * 0.05f, n);
        body *= Lerp(0.6f, 1f, Smoothstep(0.32f, 0.72f, ridges));
        body *= Lerp(0.75f, 1.05f, detail);
        body *= corner;
        body *= outer;
        body = Math.Clamp(body, 0f, 1f);

        if (layerId > 1.5f && body < 0.35f)
            return 0f;

        const float layerBands = 5f;
        var bandIdx = MathF.Floor(body * layerBands);
        bandIdx = Math.Clamp(bandIdx, 0f, layerBands - 1f);
        var stepped = bandIdx / (layerBands - 1f);

        const float edgeBands = 4f;
        var edgeBandIdx = MathF.Floor(Math.Clamp(outer, 0f, 1f) * edgeBands);
        edgeBandIdx = Math.Clamp(edgeBandIdx, 0f, edgeBands - 1f);
        var edgeStepped = edgeBandIdx / (edgeBands - 1f);

        var notch = Step(0.72f, detail) * Step(0.4f, angFold) * (1f / layerBands);
        stepped = MathF.Max(stepped - notch, 0f);
        stepped *= edgeStepped;

        return stepped;
    }

    public static Vector2[] BuildMidLayerContour(
        Vector2 worldCenter,
        float worldRadius,
        int seed,
        float density,
        int segments = NavContourSegments)
    {
        var points = new Vector2[segments];
        BuildMidLayerContour(worldCenter, worldRadius, seed, density, points.AsSpan(), segments);
        return points;
    }

    public static void BuildMidLayerContour(
        Vector2 worldCenter,
        float worldRadius,
        int seed,
        float density,
        Span<Vector2> points,
        int segments = NavContourSegments)
    {
        var step = worldRadius / NavContourSteps;
        for (var i = 0; i < segments; i++)
        {
            var angle = i * MathF.Tau / segments;
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var edgeR = worldRadius * 0.35f;
            const float threshold = 0.12f;

            for (var r = worldRadius * 0.2f; r <= worldRadius; r += step)
            {
                var p = dir * (r / worldRadius);
                if (SamplePresence(p, seed, density, 1f, worldRadius) > threshold)
                    edgeR = r;
            }

            points[i] = dir * edgeR;
        }
    }

    private static float Fract(float x) => x - MathF.Floor(x);

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static float Smoothstep(float edge0, float edge1, float x)
    {
        var t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float Step(float edge, float x) => x < edge ? 0f : 1f;
}
