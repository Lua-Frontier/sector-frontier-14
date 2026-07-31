// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Client.Parallax;
using Content.Shared._Lua.SpaceHazards;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Lua.SpaceHazards;

public sealed class PixelPlanetOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> StarBlobsShader = "PixelStarBlobs";
    private static readonly ProtoId<ShaderPrototype> StarShader = "PixelStar";
    private static readonly ProtoId<ShaderPrototype> StarFlaresShader = "PixelStarFlares";
    private static readonly ProtoId<ShaderPrototype> BlackHoleShader = "PixelBlackHole";
    private static readonly ProtoId<ShaderPrototype> LandRiversShader = "PixelLandRivers";
    private static readonly ProtoId<ShaderPrototype> TerranDryShader = "PixelTerranDry";
    private static readonly ProtoId<ShaderPrototype> LandMassesShader = "PixelLandMasses";
    private static readonly ProtoId<ShaderPrototype> NoAtmosphereShader = "PixelNoAtmosphere";
    private static readonly ProtoId<ShaderPrototype> GasPlanetShader = "PixelGasPlanet";
    private static readonly ProtoId<ShaderPrototype> GasPlanetLayersShader = "PixelGasPlanetLayers";
    private static readonly ProtoId<ShaderPrototype> IceWorldShader = "PixelIceWorld";
    private static readonly ProtoId<ShaderPrototype> LavaWorldShader = "PixelLavaWorld";
    private const float BlackHoleDrawScale = 3f;
    private const float StarOuterPassScale = 3f;
    private const float AnimSpeedScale = 0.15f;
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;

    private readonly IEntityManager _entManager;
    private readonly IPrototypeManager _prototypes;
    private readonly IGameTiming _timing;
    private readonly SharedTransformSystem _transform;
    private readonly Dictionary<string, ShaderInstance> _shaders = new();

    public PixelPlanetOverlay(IEntityManager entManager, IPrototypeManager prototypes)
    {
        _entManager = entManager;
        _prototypes = prototypes;
        _timing = IoCManager.Resolve<IGameTiming>();
        _transform = entManager.System<SharedTransformSystem>();
        ZIndex = ParallaxSystem.ParallaxZIndex + 1;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.MapId == MapId.Nullspace)
            return;

        var handle = args.WorldHandle;
        var aabb = args.WorldAABB;
        var timeBase = (float)_timing.CurTime.TotalSeconds;

        var celestialQuery = _entManager.EntityQueryEnumerator<SectorCelestialBodyComponent, TransformComponent>();
        while (celestialQuery.MoveNext(out _, out var body, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var pos = _transform.GetWorldPosition(xform);
            var radius = MathF.Max(body.SpriteRadius, 1f);
            var cullPad = body.Kind == CelestialKind.BlackHole
                ? radius * BlackHoleDrawScale
                : radius * StarOuterPassScale;

            if (!Intersects(aabb, pos, cullPad))
                continue;

            var time = timeBase + body.Seed;
            switch (body.Kind)
            {
                case CelestialKind.Star:
                    DrawStar(handle, pos, radius, body.Pixels, body.Seed, body.Palette, time);
                    break;
                case CelestialKind.BlackHole:
                    DrawBlackHole(handle, pos, radius, body.Pixels, body.Seed, body.Palette, time);
                    break;
            }
        }

        var planetQuery = _entManager.EntityQueryEnumerator<SectorBackgroundPlanetComponent, TransformComponent>();
        while (planetQuery.MoveNext(out _, out var planet, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var pos = _transform.GetWorldPosition(xform);
            var radius = MathF.Max(planet.SpriteRadius, 1f);
            if (!Intersects(aabb, pos, radius))
                continue;

            DrawPlanet(handle, pos, planet, timeBase + planet.Seed);
        }

        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(null);
    }

    private void DrawStar(
        DrawingHandleWorld handle,
        Vector2 pos,
        float radius,
        float pixels,
        float seed,
        byte palette,
        float time)
    {
        var surfaceSize = new Vector2(radius * 2f, radius * 2f);
        var outerSize = surfaceSize * StarOuterPassScale;
        var outerPixels = pixels * StarOuterPassScale;
        var blobs = GetShader(StarBlobsShader);
        blobs.SetParameter("time", time);
        blobs.SetParameter("pixels", outerPixels);
        blobs.SetParameter("seed", seed);
        blobs.SetParameter("rotation", 0f);
        blobs.SetParameter("time_speed", 0.05f * AnimSpeedScale);
        blobs.SetParameter("size", 4.93f);
        blobs.SetParameter("circle_amount", 2f);
        blobs.SetParameter("circle_size", 1f);
        PixelPlanetPalettes.ApplyStarBlobs(blobs, palette);
        DrawQuad(handle, blobs, pos, outerSize);
        var star = GetShader(StarShader);
        star.SetParameter("time", time);
        star.SetParameter("pixels", pixels);
        star.SetParameter("seed", seed);
        star.SetParameter("rotation", 0f);
        star.SetParameter("time_speed", 0.05f * AnimSpeedScale);
        star.SetParameter("should_dither", 0f);
        star.SetParameter("tiles", 1f);
        PixelPlanetPalettes.ApplyStar(star, palette);
        DrawQuad(handle, star, pos, surfaceSize);
        var flares = GetShader(StarFlaresShader);
        flares.SetParameter("time", time);
        flares.SetParameter("pixels", outerPixels);
        flares.SetParameter("seed", seed);
        flares.SetParameter("rotation", 0f);
        flares.SetParameter("time_speed", 0.05f * AnimSpeedScale);
        flares.SetParameter("should_dither", 0f);
        flares.SetParameter("size", 1.6f);
        flares.SetParameter("octaves", 4f);
        flares.SetParameter("storm_width", 0.3f);
        flares.SetParameter("storm_dither_width", 0f);
        flares.SetParameter("flare_scale", 1f);
        flares.SetParameter("circle_amount", 2f);
        flares.SetParameter("circle_scale", 1f);
        PixelPlanetPalettes.ApplyStarFlares(flares, palette);
        DrawQuad(handle, flares, pos, outerSize);
    }

    private void DrawBlackHole(
        DrawingHandleWorld handle,
        Vector2 pos,
        float radius,
        float pixels,
        float seed,
        byte palette,
        float time)
    {
        var drawSize = new Vector2(radius * 2f * BlackHoleDrawScale, radius * 2f * BlackHoleDrawScale);
        var shader = GetShader(BlackHoleShader);
        shader.SetParameter("time", time);
        shader.SetParameter("pixels", pixels);
        shader.SetParameter("pixels_ring", pixels);
        shader.SetParameter("seed", seed);
        shader.SetParameter("rotation", 0.766f);
        shader.SetParameter("time_speed", 0.2f * AnimSpeedScale);
        shader.SetParameter("should_dither", 0f);
        shader.SetParameter("disk_width", 0.065f);
        shader.SetParameter("ring_perspective", 14f);
        shader.SetParameter("disk_size", 6.598f);
        shader.SetParameter("octaves", 3f);
        shader.SetParameter("hole_radius", 0.247f);
        shader.SetParameter("hole_light_width", 0.028f);
        PixelPlanetPalettes.ApplyBlackHole(shader, palette);
        DrawQuad(handle, shader, pos, drawSize);
    }

    private void DrawPlanet(
        DrawingHandleWorld handle,
        Vector2 pos,
        SectorBackgroundPlanetComponent planet,
        float time)
    {
        var radius = MathF.Max(planet.SpriteRadius, 1f);
        var drawSize = new Vector2(radius * 2f, radius * 2f);
        var light = new Vector2(planet.LightOriginX, planet.LightOriginY);
        var pixels = planet.Pixels;
        var seed = planet.Seed;
        var rotation = planet.Rotation;

        switch (planet.PlanetKind)
        {
            case PixelPlanetKind.LandRivers:
                {
                    var s = GetShader(LandRiversShader);
                    SetCommonPlanet(s, time, pixels, seed, rotation, 0.2f * AnimSpeedScale, light);
                    s.SetParameter("size", 4.6f);
                    s.SetParameter("octaves", 6f);
                    s.SetParameter("river_cutoff", 0.368f);
                    s.SetParameter("dither_size", 0f);
                    s.SetParameter("light_border_1", 0.4f);
                    s.SetParameter("light_border_2", 0.5f);
                    s.SetParameter("cloud_cover", 0.47f);
                    s.SetParameter("stretch", 2f);
                    s.SetParameter("cloud_curve", 1.3f);
                    s.SetParameter("cloud_size", 7.315f);
                    s.SetParameter("cloud_octaves", 2f);
                    s.SetParameter("cloud_seed", seed + 1.5f);
                    s.SetParameter("time_speed_clouds", 0.1f * AnimSpeedScale);
                    PixelPlanetPalettes.ApplyPlanet(s, planet.PlanetKind, planet.PaletteIndex);
                    DrawQuad(handle, s, pos, drawSize);
                    break;
                }
            case PixelPlanetKind.TerranDry:
                {
                    var s = GetShader(TerranDryShader);
                    SetCommonPlanet(s, time, pixels, seed, rotation, 0.1f * AnimSpeedScale, light);
                    s.SetParameter("size", 8f);
                    s.SetParameter("octaves", 3f);
                    s.SetParameter("dither_size", 0f);
                    s.SetParameter("light_distance1", 0.362f);
                    s.SetParameter("light_distance2", 0.525f);
                    PixelPlanetPalettes.ApplyPlanet(s, planet.PlanetKind, planet.PaletteIndex);
                    DrawQuad(handle, s, pos, drawSize);
                    break;
                }
            case PixelPlanetKind.LandMasses:
                {
                    var s = GetShader(LandMassesShader);
                    SetCommonPlanet(s, time, pixels, seed, rotation, 0.1f * AnimSpeedScale, light);
                    s.SetParameter("size", 5.228f);
                    s.SetParameter("octaves", 3f);
                    s.SetParameter("dither_size", 0f);
                    s.SetParameter("light_border_1", 0.4f);
                    s.SetParameter("light_border_2", 0.6f);
                    s.SetParameter("land_cutoff", 0.633f);
                    s.SetParameter("land_size", 4.292f);
                    s.SetParameter("land_octaves", 6f);
                    s.SetParameter("land_seed", seed + 2f);
                    s.SetParameter("land_time_speed", 0.2f * AnimSpeedScale);
                    s.SetParameter("land_light_border_1", 0.32f);
                    s.SetParameter("land_light_border_2", 0.534f);
                    s.SetParameter("cloud_cover", 0.515f);
                    s.SetParameter("stretch", 2f);
                    s.SetParameter("cloud_curve", 1.3f);
                    s.SetParameter("cloud_size", 7.745f);
                    s.SetParameter("cloud_octaves", 2f);
                    s.SetParameter("cloud_seed", seed + 1.5f);
                    s.SetParameter("time_speed_clouds", 0.1f * AnimSpeedScale);
                    PixelPlanetPalettes.ApplyPlanet(s, planet.PlanetKind, planet.PaletteIndex);
                    DrawQuad(handle, s, pos, drawSize);
                    break;
                }
            case PixelPlanetKind.NoAtmosphere:
                {
                    var s = GetShader(NoAtmosphereShader);
                    SetCommonPlanet(s, time, pixels, seed, rotation, 0.4f * AnimSpeedScale, light);
                    s.SetParameter("size", 8f);
                    s.SetParameter("octaves", 4f);
                    s.SetParameter("dither_size", 0f);
                    s.SetParameter("light_border_1", 0.615f);
                    s.SetParameter("light_border_2", 0.729f);
                    s.SetParameter("crater_size", 5f);
                    s.SetParameter("crater_seed", seed + 3f);
                    s.SetParameter("crater_light_border", 0.465f);
                    PixelPlanetPalettes.ApplyPlanet(s, planet.PlanetKind, planet.PaletteIndex);
                    DrawQuad(handle, s, pos, drawSize);
                    break;
                }
            case PixelPlanetKind.GasPlanet:
                {
                    var s = GetShader(GasPlanetShader);
                    SetCommonPlanet(s, time, pixels, seed, rotation, 0.7f * AnimSpeedScale, light);
                    s.SetParameter("size", 9f);
                    s.SetParameter("octaves", 5f);
                    s.SetParameter("cloud_cover", 0.538f);
                    s.SetParameter("stretch", 1f);
                    s.SetParameter("cloud_curve", 1.3f);
                    s.SetParameter("light_border_1", 0.439f);
                    s.SetParameter("light_border_2", 0.746f);
                    s.SetParameter("inner_cover", 0f);
                    s.SetParameter("inner_time_speed", 0.7f * AnimSpeedScale);
                    s.SetParameter("inner_stretch", 1f);
                    s.SetParameter("inner_curve", 1.3f);
                    s.SetParameter("inner_border_1", 0.52f);
                    s.SetParameter("inner_border_2", 0.62f);
                    s.SetParameter("outer_time_speed", 0.7f * AnimSpeedScale);
                    PixelPlanetPalettes.ApplyPlanet(s, planet.PlanetKind, planet.PaletteIndex);
                    DrawQuad(handle, s, pos, drawSize);
                    break;
                }
            case PixelPlanetKind.GasPlanetLayers:
                {
                    var s = GetShader(GasPlanetLayersShader);
                    SetCommonPlanet(s, time, pixels, seed, rotation, 0.05f * AnimSpeedScale, light);
                    s.SetParameter("size", 8f);
                    s.SetParameter("octaves", 8f);
                    s.SetParameter("dither_size", 0f);
                    s.SetParameter("bands", 1f);
                    s.SetParameter("ring_width", 0.127f);
                    s.SetParameter("ring_perspective", 6f);
                    s.SetParameter("ring_scale", 6f);
                    s.SetParameter("ring_rotation", -0.5f);
                    s.SetParameter("ring_time_speed", 0.2f * AnimSpeedScale);
                    s.SetParameter("ring_size", 5f);
                    s.SetParameter("ring_octaves", 4f);
                    s.SetParameter("ring_seed", seed + 4f);
                    s.SetParameter("full_scale", 1f);
                    PixelPlanetPalettes.ApplyPlanet(s, planet.PlanetKind, planet.PaletteIndex);
                    DrawQuad(handle, s, pos, drawSize);
                    break;
                }
            case PixelPlanetKind.IceWorld:
                {
                    var s = GetShader(IceWorldShader);
                    SetCommonPlanet(s, time, pixels, seed, rotation, 0.2f * AnimSpeedScale, light);
                    s.SetParameter("size", 4.6f);
                    s.SetParameter("octaves", 6f);
                    s.SetParameter("dither_size", 0f);
                    s.SetParameter("light_border_1", 0.4f);
                    s.SetParameter("light_border_2", 0.5f);
                    s.SetParameter("lake_cutoff", 0.55f);
                    s.SetParameter("lake_size", 10f);
                    s.SetParameter("lake_seed", seed + 2f);
                    s.SetParameter("lake_border_1", 0.024f);
                    s.SetParameter("lake_border_2", 0.047f);
                    s.SetParameter("cloud_cover", 0.546f);
                    s.SetParameter("stretch", 2.5f);
                    s.SetParameter("cloud_curve", 1.3f);
                    s.SetParameter("cloud_size", 4f);
                    s.SetParameter("cloud_octaves", 4f);
                    s.SetParameter("cloud_seed", seed + 1.5f);
                    s.SetParameter("time_speed_clouds", 0.1f * AnimSpeedScale);
                    PixelPlanetPalettes.ApplyPlanet(s, planet.PlanetKind, planet.PaletteIndex);
                    DrawQuad(handle, s, pos, drawSize);
                    break;
                }
            case PixelPlanetKind.LavaWorld:
                {
                    var s = GetShader(LavaWorldShader);
                    SetCommonPlanet(s, time, pixels, seed, rotation, 0.2f * AnimSpeedScale, light);
                    s.SetParameter("size", 10f);
                    s.SetParameter("octaves", 3f);
                    s.SetParameter("dither_size", 0f);
                    s.SetParameter("light_border_1", 0.4f);
                    s.SetParameter("light_border_2", 0.6f);
                    s.SetParameter("crater_size", 3.5f);
                    s.SetParameter("crater_seed", seed + 3f);
                    s.SetParameter("crater_time_speed", 0.09f * AnimSpeedScale);
                    s.SetParameter("crater_light_border", 0.4f);
                    s.SetParameter("lava_cutoff", 0.579f);
                    s.SetParameter("lava_size", 10f);
                    s.SetParameter("lava_octaves", 4f);
                    s.SetParameter("lava_seed", seed + 5f);
                    s.SetParameter("lava_time_speed", 0.2f * AnimSpeedScale);
                    s.SetParameter("lava_border_1", 0.019f);
                    s.SetParameter("lava_border_2", 0.036f);
                    PixelPlanetPalettes.ApplyPlanet(s, planet.PlanetKind, planet.PaletteIndex);
                    DrawQuad(handle, s, pos, drawSize);
                    break;
                }
        }
    }

    private static void SetCommonPlanet(
        ShaderInstance shader,
        float time,
        float pixels,
        float seed,
        float rotation,
        float timeSpeed,
        Vector2 lightOrigin)
    {
        shader.SetParameter("time", time);
        shader.SetParameter("pixels", pixels);
        shader.SetParameter("seed", seed);
        shader.SetParameter("rotation", rotation);
        shader.SetParameter("time_speed", timeSpeed);
        shader.SetParameter("light_origin", lightOrigin);
    }

    private static void DrawQuad(DrawingHandleWorld handle, ShaderInstance shader, Vector2 pos, Vector2 size)
    {
        handle.UseShader(shader);
        handle.SetTransform(Matrix3Helpers.CreateTranslation(pos));
        handle.DrawTextureRect(Texture.White, Box2.CenteredAround(Vector2.Zero, size));
    }

    private static bool Intersects(Box2 aabb, Vector2 pos, float radius)
    {
        var bounds = Box2.CenteredAround(pos, new Vector2(radius * 2f, radius * 2f));
        return aabb.Intersects(bounds);
    }
    private ShaderInstance GetShader(ProtoId<ShaderPrototype> id)
    {
        var key = id.Id;
        if (_shaders.TryGetValue(key, out var existing))
            return existing;

        var instance = _prototypes.Index(id).InstanceUnique();
        _shaders[key] = instance;
        return instance;
    }
}
