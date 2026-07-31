// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Server.Station.Components;
using Content.Shared._Lua.SpaceHazards;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Lua.SpaceHazards;

public sealed class SectorBackgroundPlanetPlacerSystem : EntitySystem
{
    public const int PaletteCount = 9;
    private static readonly PixelPlanetKind[] PlanetKinds = Enum.GetValues<PixelPlanetKind>();
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SectorPixelPlanetLightSystem _lights = default!;
    [Dependency] private readonly SectorLandmarkAnchorSystem _landmarks = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SectorBackgroundPlanetPlacerControllerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SectorBackgroundPlanetComponent, MapInitEvent>(OnPlanetMapInit);
    }

    private void OnStartup(EntityUid uid, SectorBackgroundPlanetPlacerControllerComponent component, ComponentStartup args)
    {
        TrySpawn(uid, component);
    }

    public void InitializePlacer(EntityUid mapUid)
    {
        if (!TryComp(mapUid, out SectorBackgroundPlanetPlacerControllerComponent? placer))
            return;

        TrySpawn(mapUid, placer);
    }

    private void TrySpawn(EntityUid mapUid, SectorBackgroundPlanetPlacerControllerComponent placer)
    {
        if (placer.Spawned)
            return;

        if (!TryComp<MapComponent>(mapUid, out var map))
            return;

        if (!_prototypes.HasIndex<EntityPrototype>(placer.PlanetPrototype))
        {
            Log.Error($"Background planet prototype '{placer.PlanetPrototype}' missing");
            return;
        }

        var count = _random.Next(placer.MinCount, placer.MaxCount + 1);
        var stationCenters = CollectStationCenters(map.MapId);
        var spawned = 0;

        for (var attempt = 0; attempt < count * 8 && spawned < count; attempt++)
        {
            var angle = _random.NextFloat() * MathF.Tau;
            var dist = _random.NextFloat(placer.MinDistance, placer.MaxDistance);
            var pos = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;

            if (OverlapsStation(pos, placer.MinStationClearance, stationCenters))
                continue;

            var ent = Spawn(placer.PlanetPrototype, new MapCoordinates(pos, map.MapId));
            if (TryComp<SectorBackgroundPlanetComponent>(ent, out var planet))
            {
                RandomizeShaderParams(ent, planet, map.MapId);
                Dirty(ent, planet);
                _lights.ApplyPlanet(ent, planet);
                _landmarks.LockToMap(ent);
            }

            spawned++;
        }

        placer.Spawned = true;
        Log.Info($"Spawned {spawned} background planets on {ToPrettyString(mapUid)}");
    }

    private void OnPlanetMapInit(EntityUid uid, SectorBackgroundPlanetComponent planet, MapInitEvent args)
    {
        if (!planet.VisualsInitialized)
        {
            var mapId = Transform(uid).MapID;
            RandomizeShaderParams(uid, planet, mapId);
        }

        Dirty(uid, planet);
        _lights.ApplyPlanet(uid, planet);
        _landmarks.LockToMap(uid);
    }

    private void RandomizeShaderParams(EntityUid uid, SectorBackgroundPlanetComponent planet, MapId mapId)
    {
        planet.PlanetKind = _random.Pick(PlanetKinds);
        planet.Seed = 0.01f + _random.NextFloat() * 9.99f;
        var mix = uid.Id * 2654435761u ^ (uint) mapId.GetHashCode() ^ (uint) _random.Next();
        planet.PaletteIndex = (byte) (mix % (uint) PaletteCount);
        planet.Rotation = _random.NextFloat() * MathF.Tau;
        planet.LightOriginX = _random.NextFloat(0.2f, 0.8f);
        planet.LightOriginY = _random.NextFloat(0.2f, 0.8f);
        planet.VisualsInitialized = true;
    }

    private List<Vector2> CollectStationCenters(MapId mapId)
    {
        var centers = new List<Vector2>();
        var query = EntityQueryEnumerator<StationDataComponent>();
        while (query.MoveNext(out _, out var data))
        {
            foreach (var gridUid in data.Grids)
            {
                if (!TryComp(gridUid, out TransformComponent? xform) || xform.MapID != mapId)
                    continue;

                centers.Add(_transform.GetWorldPosition(xform));
            }
        }

        return centers;
    }

    private static bool OverlapsStation(Vector2 pos, float clearance, List<Vector2> centers)
    {
        var clearSq = clearance * clearance;
        foreach (var c in centers)
        {
            if ((pos - c).LengthSquared() < clearSq)
                return true;
        }

        return false;
    }
}
