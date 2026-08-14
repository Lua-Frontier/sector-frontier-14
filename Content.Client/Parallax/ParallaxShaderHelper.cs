using System.Numerics;
using Content.Client.Parallax.Managers;
using Content.Shared.CCVar;
using Content.Shared.Parallax;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Maths;

namespace Content.Client.Parallax;

public static class ParallaxShaderHelper
{
    public const float FallbackViewScale = 2f;

    public const int QualityVeryLow = 0;
    public const int QualityLow = 1;
    public const int QualityMedium = 2;
    public const int QualityHigh = 3;

    private readonly record struct EffectiveParallax(
        ParallaxPrototype Proto,
        int Quality,
        bool ImagesEnabled,
        float Seed,
        Vector2 ScrollSpeed,
        float StarDensity,
        Color BaseColor,
        Color NebulaColor,
        Color AccentColor,
        Color HorizonColor,
        int LayerCount,
        int LayerCountLQ);

    private static float ScreenStableScale(float worldScale, float zoom, float viewScale)
        => worldScale / Math.Max(zoom, 0.001f) / Math.Max(viewScale, 0.001f);

    public static void Draw(
        DrawingHandleWorld handle,
        Box2 worldBounds,
        IParallaxManager manager,
        IConfigurationManager configuration,
        ParallaxPrototype prototype,
        float time,
        Vector2 eyeWorld,
        float worldPerPixel,
        float zoom = 1f,
        float? viewScale = null)
    {
        var p = Resolve(prototype, configuration);
        zoom = Math.Max(zoom, 0.001f);
        var vs = Math.Max(viewScale ?? prototype.ViewScale, 0.001f);
        var drawImages = p.ImagesEnabled && p.Proto.ImageLayers.Count > 0;
        var drawSky = p.Quality > QualityVeryLow;

        if (!drawSky)
        {
            handle.DrawRect(worldBounds, p.BaseColor);
            if (drawImages)
                DrawImageLayersWorld(handle, manager, p.Proto, worldBounds, eyeWorld, time);
            handle.SetTransform(Matrix3x2.Identity);
            handle.UseShader(null);
            return;
        }

        if (p.StarDensity <= 0f && !drawImages && p.Proto.Style != ParallaxStyle.Cosmic)
        {
            handle.DrawRect(worldBounds, p.BaseColor);
            handle.SetTransform(Matrix3x2.Identity);
            handle.UseShader(null);
            return;
        }

        if (p.Proto.Style == ParallaxStyle.Cosmic)
            DrawCosmic(handle, worldBounds, manager, p, time);
        else
            DrawTelescope(handle, worldBounds, manager, p, time, eyeWorld, worldPerPixel, zoom, vs);

        if (drawImages)
            DrawImageLayersWorld(handle, manager, p.Proto, worldBounds, eyeWorld, time);

        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(null);
    }

    public static void Draw(
        DrawingHandleScreen handle,
        UIBox2 bounds,
        IParallaxManager manager,
        IConfigurationManager configuration,
        ParallaxPrototype prototype,
        float time,
        Vector2 eyeWorld,
        float? viewScale = null)
    {
        var p = Resolve(prototype, configuration);
        var vs = Math.Max(viewScale ?? prototype.ViewScale, 0.001f);
        var drawImages = p.ImagesEnabled && p.Proto.ImageLayers.Count > 0;
        var drawSky = p.Quality > QualityVeryLow;

        if (!drawSky)
        {
            handle.DrawRect(bounds, p.BaseColor);
            if (drawImages)
                DrawImageLayersScreen(handle, manager, p.Proto, bounds, time);
            handle.UseShader(null);
            return;
        }

        if (p.StarDensity <= 0f && !drawImages && p.Proto.Style != ParallaxStyle.Cosmic)
        {
            handle.DrawRect(bounds, p.BaseColor);
            handle.UseShader(null);
            return;
        }

        const float worldPerPixel = 1f;
        var zoom = Math.Max(prototype.UiZoom, 0.001f);

        if (p.Proto.Style == ParallaxStyle.Cosmic)
            DrawCosmic(handle, bounds, manager, p, time);
        else
            DrawTelescope(handle, bounds, manager, p, time, eyeWorld, worldPerPixel, zoom, vs);

        if (drawImages)
            DrawImageLayersScreen(handle, manager, p.Proto, bounds, time);

        handle.UseShader(null);
    }

    private static EffectiveParallax Resolve(ParallaxPrototype prototype, IConfigurationManager configuration)
    {
        var quality = Math.Clamp(configuration.GetCVar(CCVars.ParallaxQuality), QualityVeryLow, QualityHigh);
        var starsEnabled = configuration.GetCVar(CCVars.ParallaxStarsEnabled);
        var imagesEnabled = configuration.GetCVar(CCVars.ParallaxImagesEnabled);
        var scrollEnabled = configuration.GetCVar(CCVars.ParallaxScrollEnabled);
        var densityMul = Math.Clamp(configuration.GetCVar(CCVars.ParallaxStarDensity), 0f, 1.5f);

        var scroll = quality >= QualityLow && scrollEnabled ? prototype.ScrollSpeed : Vector2.Zero;
        var stars = !starsEnabled || quality <= QualityVeryLow ? 0f : prototype.StarDensity * densityMul;
        if (quality == QualityLow)
            stars *= 0.85f;

        return new EffectiveParallax(
            prototype,
            quality,
            imagesEnabled,
            prototype.Seed,
            scroll,
            stars,
            prototype.BaseColor,
            prototype.NebulaColor,
            prototype.AccentColor,
            prototype.HorizonColor,
            prototype.LayerCount,
            prototype.LayerCountLQ);
    }

    private static void DrawTelescope(
        DrawingHandleWorld handle,
        Box2 worldBounds,
        IParallaxManager manager,
        EffectiveParallax p,
        float time,
        Vector2 eyeWorld,
        float worldPerPixel,
        float zoom,
        float viewScale)
    {
        var density = Math.Clamp(p.StarDensity, 0f, 1.5f);
        var proto = p.Proto;

        var bg = manager.GetTelescopeBackground();
        ConfigureTelescopeBackground(
            bg,
            manager,
            p,
            eyeWorld,
            worldBounds,
            proto.BgSlowness,
            ScreenStableScale(proto.BgWorldScale, zoom, viewScale),
            proto.BgNebulaMul);
        handle.UseShader(bg);
        handle.DrawTextureRect(Texture.White, worldBounds);
        handle.UseShader(null);

        if (density > 0f)
            DrawStarPass(handle, manager, p, time, eyeWorld, worldBounds, worldPerPixel, zoom, viewScale, density);

        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(null);
    }

    private static void DrawTelescope(
        DrawingHandleScreen handle,
        UIBox2 bounds,
        IParallaxManager manager,
        EffectiveParallax p,
        float time,
        Vector2 eyeWorld,
        float worldPerPixel,
        float zoom,
        float viewScale)
    {
        var screenBounds = new Box2(bounds.Left, bounds.Bottom, bounds.Right, bounds.Top);
        var density = Math.Clamp(p.StarDensity, 0f, 1.5f);
        var proto = p.Proto;

        var bg = manager.GetTelescopeBackground();
        ConfigureTelescopeBackground(
            bg,
            manager,
            p,
            eyeWorld,
            screenBounds,
            proto.BgSlowness,
            ScreenStableScale(proto.BgWorldScale, zoom, viewScale),
            proto.BgNebulaMul);
        handle.UseShader(bg);
        handle.DrawTextureRect(Texture.White, bounds);
        handle.UseShader(null);

        if (density > 0f)
            DrawStarPassScreen(handle, bounds, screenBounds, manager, p, time, eyeWorld, worldPerPixel, zoom, viewScale, density);

        handle.UseShader(null);
    }

    private static void DrawImageLayersWorld(
        DrawingHandleWorld handle,
        IParallaxManager manager,
        ParallaxPrototype proto,
        Box2 worldBounds,
        Vector2 eyeWorld,
        float time)
    {
        if (proto.ImageLayers.Count == 0)
            return;

        foreach (var layer in proto.ImageLayers)
        {
            var tex = manager.GetImageTexture(layer.Path);
            handle.UseShader(manager.GetNamedShader(layer.Shader));

            var size = tex.Size / (float) EyeManager.PixelsPerMeter * layer.Scale;
            var home = layer.WorldHomePosition + manager.ParallaxAnchor;
            var scrolled = layer.Scrolling * time;
            var originBL = (eyeWorld - home) * layer.Slowness + scrolled;
            originBL += home;
            originBL += layer.WorldAdjustPosition;
            originBL -= size / 2f;

            if (layer.Tiled)
            {
                var flooredBL = worldBounds.BottomLeft - originBL;
                flooredBL = (flooredBL / size).Floored() * size;
                flooredBL += originBL;

                for (var x = flooredBL.X; x < worldBounds.Right; x += size.X)
                {
                    for (var y = flooredBL.Y; y < worldBounds.Top; y += size.Y)
                    {
                        handle.DrawTextureRect(tex, Box2.FromDimensions(new Vector2(x, y), size));
                    }
                }
            }
            else
            {
                handle.DrawTextureRect(tex, Box2.FromDimensions(originBL, size));
            }
        }

        handle.UseShader(null);
    }

    private static void DrawImageLayersScreen(
        DrawingHandleScreen handle,
        IParallaxManager manager,
        ParallaxPrototype proto,
        UIBox2 bounds,
        float time)
    {
        if (proto.ImageLayers.Count == 0)
            return;

        foreach (var layer in proto.ImageLayers)
        {
            var tex = manager.GetImageTexture(layer.Path);
            handle.UseShader(manager.GetNamedShader(layer.Shader));

            var size = tex.Size * layer.Scale;
            var home = layer.ControlHomePosition;
            var scrolled = layer.Scrolling * time * EyeManager.PixelsPerMeter;
            var origin = home + scrolled - size / 2f;

            if (layer.Tiled)
            {
                var floored = bounds.TopLeft - origin;
                floored = new Vector2(
                    MathF.Floor(floored.X / size.X) * size.X,
                    MathF.Floor(floored.Y / size.Y) * size.Y);
                floored += origin;

                for (var x = floored.X; x < bounds.Right; x += size.X)
                {
                    for (var y = floored.Y; y < bounds.Bottom; y += size.Y)
                    {
                        handle.DrawTextureRect(tex, UIBox2.FromDimensions(new Vector2(x, y), size));
                    }
                }
            }
            else
            {
                handle.DrawTextureRect(tex, UIBox2.FromDimensions(origin, size));
            }
        }

        handle.UseShader(null);
    }

    private static void DrawStarPass(
        DrawingHandleWorld handle,
        IParallaxManager manager,
        EffectiveParallax p,
        float time,
        Vector2 eyeWorld,
        Box2 worldBounds,
        float worldPerPixel,
        float zoom,
        float viewScale,
        float density)
    {
        var layers = p.Proto.StarLayers;
        if (p.Quality <= QualityLow)
        {
            foreach (var idx in p.Proto.LowQualityStarLayers)
            {
                if (idx < 0 || idx >= layers.Count)
                    continue;

                var shader = manager.GetTelescopeStarField(idx);
                ConfigureTelescopeStars(shader, p, time, eyeWorld, worldBounds, layers[idx], worldPerPixel, zoom, viewScale, density);
                handle.UseShader(shader);
                handle.DrawTextureRect(Texture.White, worldBounds);
            }

            return;
        }

        for (var i = 0; i < layers.Count; i++)
        {
            var shader = manager.GetTelescopeStarField(i);
            ConfigureTelescopeStars(shader, p, time, eyeWorld, worldBounds, layers[i], worldPerPixel, zoom, viewScale, density);
            handle.UseShader(shader);
            handle.DrawTextureRect(Texture.White, worldBounds);
        }
    }

    private static void DrawStarPassScreen(
        DrawingHandleScreen handle,
        UIBox2 bounds,
        Box2 screenBounds,
        IParallaxManager manager,
        EffectiveParallax p,
        float time,
        Vector2 eyeWorld,
        float worldPerPixel,
        float zoom,
        float viewScale,
        float density)
    {
        var layers = p.Proto.StarLayers;
        if (p.Quality <= QualityLow)
        {
            foreach (var idx in p.Proto.LowQualityStarLayers)
            {
                if (idx < 0 || idx >= layers.Count)
                    continue;

                var shader = manager.GetTelescopeStarField(idx);
                ConfigureTelescopeStars(shader, p, time, eyeWorld, screenBounds, layers[idx], worldPerPixel, zoom, viewScale, density);
                handle.UseShader(shader);
                handle.DrawTextureRect(Texture.White, bounds);
            }

            return;
        }

        for (var i = 0; i < layers.Count; i++)
        {
            var shader = manager.GetTelescopeStarField(i);
            ConfigureTelescopeStars(shader, p, time, eyeWorld, screenBounds, layers[i], worldPerPixel, zoom, viewScale, density);
            handle.UseShader(shader);
            handle.DrawTextureRect(Texture.White, bounds);
        }
    }

    private static void DrawCosmic(
        DrawingHandleWorld handle,
        Box2 worldBounds,
        IParallaxManager manager,
        EffectiveParallax p,
        float time)
    {
        ConfigureCosmic(manager.GetCosmicBackground(), manager, p, time);
        handle.UseShader(manager.GetCosmicBackground());
        handle.DrawTextureRect(Texture.White, worldBounds);
    }

    private static void DrawCosmic(
        DrawingHandleScreen handle,
        UIBox2 bounds,
        IParallaxManager manager,
        EffectiveParallax p,
        float time)
    {
        ConfigureCosmic(manager.GetCosmicBackground(), manager, p, time);
        handle.UseShader(manager.GetCosmicBackground());
        handle.DrawTextureRect(Texture.White, bounds);
    }

    private static void ConfigureTelescopeBackground(
        ShaderInstance shader,
        IParallaxManager manager,
        EffectiveParallax p,
        Vector2 eyeWorld,
        Box2 worldBounds,
        float slowness,
        float worldScale,
        float nebulaMul)
    {
        var proto = p.Proto;
        var nebulaIntensity = proto.NebulaIntensity * nebulaMul;
        var nebulaC = ToVector3(p.HorizonColor) * proto.HorizonColorMul;

        shader.SetParameter("world_bl", worldBounds.BottomLeft);
        shader.SetParameter("world_size", worldBounds.Size);
        shader.SetParameter("parallax_origin", eyeWorld * slowness);
        shader.SetParameter("world_scale", worldScale);
        shader.SetParameter("seed_bias", new Vector2(p.Seed * 0.017f, p.Seed * 0.011f));
        shader.SetParameter("sample_color", ToVector3(p.BaseColor) * proto.BaseColorMul);
        shader.SetParameter("nebula_color_a", ToVector3(p.AccentColor));
        shader.SetParameter("nebula_color_b", ToVector3(p.NebulaColor));
        shader.SetParameter("nebula_color_c", nebulaC);
        shader.SetParameter("nebula_color_exponent", proto.NebulaExponent);
        shader.SetParameter("nebula_color_intensity", nebulaIntensity);
        shader.SetParameter("noise_texture", manager.FireNoise);
        shader.SetParameter("nebula_texture_a", manager.WavyBlotchNoise);
        shader.SetParameter("nebula_texture_b", manager.DendriticNoiseZoomedOut);
    }

    private static void ConfigureTelescopeStars(
        ShaderInstance shader,
        EffectiveParallax p,
        float time,
        Vector2 eyeWorld,
        Box2 worldBounds,
        ParallaxStarLayerData layer,
        float worldPerPixel,
        float zoom,
        float viewScale,
        float density)
    {
        var densityFactor = Math.Clamp(density, 0.25f, 1.5f);
        var worldScale = ScreenStableScale(
            layer.WorldScale * MathHelper.Lerp(0.9f, 1.2f, densityFactor / 1.5f),
            zoom,
            viewScale);
        var cutoff = MathHelper.Lerp(layer.Cutoff + 0.015f, layer.Cutoff - 0.03f, densityFactor / 1.5f);
        var intensity = layer.Intensity * MathHelper.Lerp(0.85f, 1.25f, densityFactor / 1.5f);

        shader.SetParameter("time", time);
        shader.SetParameter("twinkle_speed", p.Proto.TwinkleSpeed);
        shader.SetParameter("world_bl", worldBounds.BottomLeft);
        shader.SetParameter("world_size", worldBounds.Size);
        shader.SetParameter("parallax_origin", eyeWorld * layer.Slowness);
        shader.SetParameter("world_scale", worldScale);
        shader.SetParameter("star_cutoff", cutoff);
        shader.SetParameter("brightness_power", layer.Power);
        shader.SetParameter("intensity", intensity);
        shader.SetParameter("world_per_pixel", Math.Max(worldPerPixel, 0.0001f));
        shader.SetParameter("size_min_px", layer.SizeMinPx);
        shader.SetParameter("size_max_px", layer.SizeMaxPx);
        shader.SetParameter("seed_bias", layer.SeedBias + new Vector2(p.Seed * 0.13f, p.Seed * 0.07f));
    }

    private static void ConfigureCosmic(
        ShaderInstance shader,
        IParallaxManager manager,
        EffectiveParallax p,
        float time)
    {
        var detail = p.Quality <= QualityLow
            ? Math.Clamp(p.LayerCountLQ, 1, 20)
            : Math.Clamp(p.Quality >= QualityHigh ? Math.Max(p.LayerCount, p.LayerCountLQ + 2) : p.LayerCount, 1, 20);

        var cosmic = p.Proto.Cosmic;
        var scroll = p.ScrollSpeed.X != 0f
            ? p.ScrollSpeed.X
            : (p.ScrollSpeed == Vector2.Zero ? 0f : cosmic.DefaultScroll);

        shader.SetParameter("time", time);
        shader.SetParameter("zoom", cosmic.Zoom);
        shader.SetParameter("brightness", cosmic.Brightness);
        shader.SetParameter("scroll_speed_factor", scroll);
        shader.SetParameter("detail_iterations", (float) detail);
        shader.SetParameter("front_star_color", ToVector3(p.AccentColor) * cosmic.FrontStarColorMul);
        shader.SetParameter("back_star_color", ToVector3(p.NebulaColor) * cosmic.BackStarColorMul);
        shader.SetParameter("color_change_influence_1", cosmic.ColorChangeInfluence1);
        shader.SetParameter("color_change_influence_2", cosmic.ColorChangeInfluence2);
        shader.SetParameter("color_change_strength_1", cosmic.ColorChangeStrength1);
        shader.SetParameter("color_change_strength_2", cosmic.ColorChangeStrength2);
        shader.SetParameter("kaliset_fractal", manager.KalisetTexture);
        shader.SetParameter("noise_texture", manager.TurbulentNoise);
    }

    private static Vector3 ToVector3(Color color) => new(color.R, color.G, color.B);
}
