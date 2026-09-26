// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Lua.Shared.AmbientSpaceEffects;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;
using Robust.Shared.Timing;

namespace Content.Lua.Client.AmbientSpaceEffects;

public sealed class AmbientSpaceNebulaBakeCache
{
    public const int MaxBakesPerTick = 6;

    private static readonly TextureSampleParameters BakeSampleParams = new()
    {
        Filter = true,
        WrapMode = TextureWrapMode.None,
    };

    private readonly IClyde _clyde;
    private readonly Dictionary<(EntityUid Uid, AmbientSpaceLayer Layer), BakeEntry> _entries = new();
    private int _lastQuality = -1;
    private GameTick _budgetTick;
    private int _bakesLeft;

    private sealed class BakeEntry
    {
        public IRenderTexture Target = default!;
        public int Seed;
        public float Radius;
        public float Density;
        public int Resolution;
        public int Quality;
    }

    public AmbientSpaceNebulaBakeCache(IClyde clyde)
    {
        _clyde = clyde;
    }

    public static void LodThresholds(int quality, out float nearDist, out float farDist)
    {
        switch (quality)
        {
            case <= 1:
                nearDist = 200f;
                farDist = 500f;
                break;
            case 2:
                nearDist = 350f;
                farDist = 700f;
                break;
            default:
                nearDist = 500f;
                farDist = 900f;
                break;
        }
    }

    public static int MaxNearPerLayer(int quality) => quality switch
    {
        <= 1 => 1,
        2 => 2,
        _ => 3,
    };

    public static int ResolutionForQuality(int quality) => quality switch
    {
        <= 1 => 128,
        2 => 192,
        _ => 256,
    };

    public void BeginTick(GameTick tick)
    {
        if (_budgetTick == tick)
            return;

        _budgetTick = tick;
        _bakesLeft = MaxBakesPerTick;
    }

    public Texture? TryGet(
        EntityUid uid,
        AmbientSpaceFieldComponent field,
        AmbientSpaceLayer layer,
        float radius,
        int quality)
    {
        EnsureQuality(quality);

        var resolution = ResolutionForQuality(quality);
        var key = (uid, layer);
        if (_entries.TryGetValue(key, out var entry)
            && entry.Seed == field.Seed
            && MathF.Abs(entry.Radius - radius) <= 0.01f
            && MathF.Abs(entry.Density - field.Density) <= 0.001f
            && entry.Resolution == resolution
            && entry.Quality == quality)
        {
            return entry.Target.Texture;
        }

        return null;
    }

    public Texture? GetOrBake(
        DrawingHandleWorld handle,
        ShaderInstance bakeShader,
        EntityUid uid,
        AmbientSpaceFieldComponent field,
        AmbientSpaceLayer layer,
        float radius,
        int quality,
        float layerId,
        float particleScale,
        float qualityF)
    {
        var existing = TryGet(uid, field, layer, radius, quality);
        if (existing != null)
            return existing;

        if (_bakesLeft <= 0)
            return null;

        EnsureQuality(quality);
        var resolution = ResolutionForQuality(quality);
        var key = (uid, layer);

        if (_entries.Remove(key, out var stale))
            stale.Target.Dispose();

        var size = new Vector2i(resolution, resolution);
        var target = _clyde.CreateRenderTarget(
            size,
            new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8),
            BakeSampleParams,
            name: $"nebula-lod-bake-{field.Seed}-{layer}-{resolution}");

        var shaderSeed = AmbientSpacePalette.ShaderSeedFromField(field.Seed);
        bakeShader.SetParameter("nebula_color", Color.White);
        bakeShader.SetParameter("seed", shaderSeed);
        bakeShader.SetParameter("density", field.Density);
        bakeShader.SetParameter("layer_alpha", 1f);
        bakeShader.SetParameter("particle_scale", particleScale);
        bakeShader.SetParameter("quality", qualityF);
        bakeShader.SetParameter("field_radius", radius);
        bakeShader.SetParameter("layer_id", layerId);
        bakeShader.SetParameter("time", 0f);
        bakeShader.SetParameter("time_speed", 0f);

        handle.RenderInRenderTarget(target, () =>
        {
            handle.UseShader(bakeShader);
            handle.SetTransform(Matrix3x2.Identity);
            handle.DrawTextureRect(
                Texture.White,
                Box2.FromDimensions(Vector2.Zero, new Vector2(resolution, resolution)));
            handle.UseShader(null);
        }, Color.Transparent);

        _entries[key] = new BakeEntry
        {
            Target = target,
            Seed = field.Seed,
            Radius = radius,
            Density = field.Density,
            Resolution = resolution,
            Quality = quality,
        };
        _bakesLeft--;
        return target.Texture;
    }

    public void Prune(HashSet<EntityUid> keep)
    {
        List<(EntityUid, AmbientSpaceLayer)>? remove = null;
        foreach (var key in _entries.Keys)
        {
            if (keep.Contains(key.Uid))
                continue;

            remove ??= new List<(EntityUid, AmbientSpaceLayer)>();
            remove.Add(key);
        }

        if (remove == null)
            return;

        foreach (var key in remove)
        {
            if (_entries.Remove(key, out var entry))
                entry.Target.Dispose();
        }
    }

    public void ClearAll()
    {
        foreach (var entry in _entries.Values)
            entry.Target.Dispose();
        _entries.Clear();
    }

    private void EnsureQuality(int quality)
    {
        if (quality == _lastQuality)
            return;

        ClearAll();
        _lastQuality = quality;
    }
}
