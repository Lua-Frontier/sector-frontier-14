// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Robust.Shared.GameObjects;

namespace Content.Shared._Lua.SpaceHazards;

public sealed class SectorPixelPlanetLightSystem : EntitySystem
{
    private const float StarRadiusMul = 10f;
    private const float StarEnergy = 9f;
    private const float BlackHoleRadiusMul = 7f;
    private const float BlackHoleEnergy = 5f;
    private const float PlanetRadiusMul = 2.75f;
    private const float PlanetEnergy = 2.4f;
    private const float PlanetOffsetMul = 0.85f;

    [Dependency] private readonly SharedPointLightSystem _lights = default!;

    public void ApplyCelestial(EntityUid uid, SectorCelestialBodyComponent body)
    {
        var light = _lights.EnsureLight(uid);
        var radius = MathF.Max(body.SpriteRadius, 1f);
        _lights.SetCastShadows(uid, false, light);
        _lights.SetEnabled(uid, true, light);
        _lights.SetSoftness(uid, 1.2f, light);
        if (body.Kind == CelestialKind.Star)
        {
            _lights.SetColor(uid, PixelPlanetLightColors.StarGlow(body.Palette), light);
            _lights.SetRadius(uid, radius * StarRadiusMul, light);
            _lights.SetEnergy(uid, StarEnergy, light);
            light.Offset = Vector2.Zero;
        }
        else
        {
            _lights.SetColor(uid, PixelPlanetLightColors.BlackHoleGlow(body.Palette), light);
            _lights.SetRadius(uid, radius * BlackHoleRadiusMul, light);
            _lights.SetEnergy(uid, BlackHoleEnergy, light);
            light.Offset = Vector2.Zero;
        }

        Dirty(uid, light);
    }

    public void ApplyPlanet(EntityUid uid, SectorBackgroundPlanetComponent planet)
    {
        var light = _lights.EnsureLight(uid);
        var radius = MathF.Max(planet.SpriteRadius, 1f);
        _lights.SetCastShadows(uid, false, light);
        _lights.SetEnabled(uid, true, light);
        _lights.SetColor(uid, PixelPlanetLightColors.PlanetSunlight, light);
        _lights.SetRadius(uid, radius * PlanetRadiusMul, light);
        _lights.SetEnergy(uid, PlanetEnergy, light);
        _lights.SetSoftness(uid, 1.4f, light);
        var dir = new Vector2(planet.LightOriginX - 0.5f, planet.LightOriginY - 0.5f);
        if (dir.LengthSquared() < 0.0001f) dir = new Vector2(0.25f, 0.25f);
        else dir = Vector2.Normalize(dir);
        light.Offset = dir * (radius * PlanetOffsetMul);
        Dirty(uid, light);
    }
}
