// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Lua.Shared.AmbientSpaceEffects;
using Content.Lua.Shared.Shuttles.Components;
using Content.Lua.Shared.SpaceHazards;
using Content.Lua.UIKit.Shuttles;
using Content.Lua.UIKit.Styles;
using Content.Shared._Mono.Radar;
using Content.Shared.Station.Components;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.GameObjects;

namespace Content.Lua.Client.Shuttles;

public sealed partial class ShuttleRadarLuaDraw : IShuttleRadarLuaDraw
{
    private const int MaxNavNebulaContours = 20;
    private const int CelestialContourSegments = 48;
    private const int MaxMapNebulaContourCache = 512;
    private const float PingCycleSeconds = 2.0f;
    private const float BlipFadeFraction = 0.65f;

    private AmbientSpaceNebulaVisibility? _nebulaVisibility;
    private readonly Dictionary<EntityUid, NebulaContourCache> _nebulaNavCache = new();
    private readonly Dictionary<EntityUid, NebulaContourCache> _nebulaMapCache = new();
    private readonly List<(EntityUid Uid, AmbientSpaceFieldComponent Field, TransformComponent Xform, Vector2 Pos, float Radius)> _nebulaFieldScratch = new();
    private readonly List<(EntityUid Uid, AmbientSpaceFieldComponent Field, Vector2 Pos, float Radius)> _mapFieldScratch = new();
    private readonly Vector2[] _celestialContourScratch = new Vector2[CelestialContourSegments];
    private static readonly Vector2[] EllipseVerts = new Vector2[8];
    private Vector2[] _nebulaFillScratch = Array.Empty<Vector2>();
    private Vector2[] _mapNebulaFillScratch = Array.Empty<Vector2>();

    public ShuttleRadarLuaDraw()
    {
    }

    private AmbientSpaceNebulaVisibility NebulaVisibility
    {
        get
        {
            if (_nebulaVisibility != null)
                return _nebulaVisibility;

            var ent = IoCManager.Resolve<IEntityManager>();
            _nebulaVisibility = new AmbientSpaceNebulaVisibility(
                ent,
                ent.System<SharedMapSystem>(),
                IoCManager.Resolve<IPrototypeManager>());
            return _nebulaVisibility;
        }
    }

    private sealed class NebulaContourCache
    {
        public int Seed;
        public float Radius;
        public float Density;
        public Vector2[] Points = Array.Empty<Vector2>();
    }

    public bool IsRadarBlipIconDrawnElsewhere(IEntityManager entManager, EntityUid uid, bool showIff)
    {
        if (IsSpaceHazardRadarIconEntity(entManager, uid))
            return true;

        if (!showIff || !entManager.HasComponent<MapGridComponent>(uid))
            return false;

        if (entManager.TryGetComponent<RadarBlipIconComponent>(uid, out var gridIcon) && gridIcon.Icon != default)
            return true;

        return entManager.TryGetComponent<StationMemberComponent>(uid, out var member)
               && entManager.TryGetComponent<RadarBlipIconComponent>(member.Station, out var stationIcon)
               && stationIcon.Icon != default;
    }

    private static bool IsSpaceHazardRadarIconEntity(IEntityManager entManager, EntityUid uid)
    {
        if (entManager.HasComponent<SectorCelestialBodyComponent>(uid))
            return true;

        return entManager.TryGetComponent<AmbientSpaceFieldComponent>(uid, out var field) && field.HasWeather;
    }
}
