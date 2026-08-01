// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Shared._Lua.SpaceHazards;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Lua.AmbientSpaceEffects;

public sealed class NebulaWeatherOverlay : Overlay
{
    private readonly IEntityManager _entities;
    private readonly IPlayerManager _players;
    private readonly IPrototypeManager _prototypes;
    private readonly IGameTiming _timing;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public NebulaWeatherOverlay(IEntityManager entities, IPrototypeManager prototypes)
    {
        _entities = entities;
        _prototypes = prototypes;
        _players = IoCManager.Resolve<IPlayerManager>();
        _timing = IoCManager.Resolve<IGameTiming>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!TryGetLocalPresence(out var presence))
            return;

        var handle = args.ScreenHandle;
        var size = args.Viewport.Size;
        var time = (float) _timing.CurTime.TotalSeconds;
        handle.SetTransform(Matrix3x2.Identity);

        if (presence.ActiveWeathers.Count == 0)
        {
            if (_prototypes.TryIndex(presence.Weather, out NebulaWeatherPrototype? fallback))
                DrawWeather(handle, size, time, fallback, Math.Clamp(presence.Intensity, 0.2f, 1f));
            return;
        }

        for (var i = 0; i < presence.ActiveWeathers.Count; i++)
        {
            if (!_prototypes.TryIndex(presence.ActiveWeathers[i], out NebulaWeatherPrototype? weather))
                continue;

            var intensity = i < presence.ActiveIntensities.Count ? presence.ActiveIntensities[i] : 1f;
            DrawWeather(handle, size, time, weather, Math.Clamp(intensity, 0.2f, 1f));
        }
    }

    private static void DrawWeather(
        DrawingHandleScreen handle,
        Vector2i size,
        float time,
        NebulaWeatherPrototype weather,
        float intensity)
    {
        switch (weather.Kind)
        {
            case NebulaWeatherKind.Lightning:
                DrawLightning(handle, size, time, intensity);
                break;
            case NebulaWeatherKind.EmpStorm:
                DrawEmpNoise(handle, size, time, intensity);
                break;
            case NebulaWeatherKind.Veil:
                DrawVeil(handle, size, time, intensity);
                break;
        }
    }

    private bool TryGetLocalPresence(out NebulaPresenceComponent presence)
    {
        presence = default!;

        if (_players.LocalEntity is not { } player ||
            !_entities.TryGetComponent(player, out TransformComponent? xform) ||
            xform.GridUid is not { } grid ||
            !_entities.TryGetComponent(grid, out NebulaPresenceComponent? foundPresence))
        {
            return false;
        }

        presence = foundPresence;
        return true;
    }

    private static void DrawLightning(DrawingHandleScreen handle, Vector2i size, float time, float intensity)
    {
        var cycle = time % 4.2f;
        if (cycle > 0.22f)
            return;

        var alpha = (1f - cycle / 0.22f) * intensity;
        handle.DrawRect(UIBox2.FromDimensions(Vector2.Zero, size), new Color(1f, 0.2f, 0.08f, 0.045f * alpha));

        var seed = MathF.Floor(time / 4.2f) * 1.713f;
        var previous = new Vector2(size.X * (0.25f + Hash(seed) * 0.5f), -8f);
        for (var i = 1; i <= 7; i++)
        {
            var next = new Vector2(
                previous.X + (Hash(seed + i * 2.1f) - 0.5f) * size.X * 0.12f,
                size.Y * i / 8f);
            handle.DrawLine(previous, next, new Color(1f, 0.82f, 0.62f, 0.7f * alpha));
            handle.DrawLine(previous + Vector2.One, next + Vector2.One, new Color(1f, 0.08f, 0.03f, 0.25f * alpha));
            previous = next;
        }
    }

    private static void DrawEmpNoise(DrawingHandleScreen handle, Vector2i size, float time, float intensity)
    {
        for (var i = 0; i < 7; i++)
        {
            var y = Hash(MathF.Floor(time * 9f) + i * 3.7f) * size.Y;
            var width = size.X * (0.12f + Hash(i + time) * 0.3f);
            var x = Hash(i * 8.3f + time * 2f) * (size.X - width);
            handle.DrawRect(UIBox2.FromDimensions(new Vector2(x, y), new Vector2(width, 1f)), new Color(0.35f, 0.9f, 1f, 0.13f * intensity));
        }
    }

    private static void DrawVeil(DrawingHandleScreen handle, Vector2i size, float time, float intensity)
    {
        handle.DrawRect(UIBox2.FromDimensions(Vector2.Zero, size), new Color(0.02f, 0.025f, 0.04f, 0.08f * intensity));
        for (var i = 0; i < 18; i++)
        {
            var y = Hash(i * 4.1f + MathF.Floor(time * 4f)) * size.Y;
            handle.DrawLine(new Vector2(0f, y), new Vector2(size.X, y), new Color(0.65f, 0.72f, 0.82f, 0.025f * intensity));
        }
    }

    private static float Hash(float value)
        => value == 0f ? 0f : MathF.Abs(MathF.Sin(value * 12.9898f) * 43758.5453f) % 1f;
}
