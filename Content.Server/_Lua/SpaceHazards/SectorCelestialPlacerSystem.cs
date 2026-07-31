// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Shared._Lua.SpaceHazards;
using Content.Shared.Radiation.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Lua.SpaceHazards;

public sealed class SectorCelestialPlacerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SectorPixelPlanetLightSystem _lights = default!;
    [Dependency] private readonly SectorLandmarkAnchorSystem _landmarks = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SectorCelestialPlacerControllerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SectorCelestialBodyComponent, MapInitEvent>(OnBodyMapInit);
    }

    private void OnStartup(EntityUid uid, SectorCelestialPlacerControllerComponent component, ComponentStartup args)
    { TrySpawn(uid, component); }

    public void InitializePlacer(EntityUid mapUid)
    {
        if (!TryComp(mapUid, out SectorCelestialPlacerControllerComponent? placer)) return;
        TrySpawn(mapUid, placer);
    }

    private void TrySpawn(EntityUid mapUid, SectorCelestialPlacerControllerComponent placer)
    {
        if (placer.Spawned) return;
        if (!TryComp<MapComponent>(mapUid, out var map)) return;
        var kind = placer.ForceKind ?? (_random.Prob(0.5f) ? CelestialKind.Star : CelestialKind.BlackHole);
        var proto = kind == CelestialKind.Star ? placer.StarPrototype : placer.BlackHolePrototype;
        if (!_prototypes.HasIndex<EntityPrototype>(proto))
        {
            Log.Error($"Celestial prototype '{proto}' missing for map {ToPrettyString(mapUid)}");
            return;
        }
        var angle = _random.NextFloat() * MathF.Tau;
        var pos = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * placer.SpawnDistance;
        var ent = Spawn(proto, new MapCoordinates(pos, map.MapId));
        if (!TryComp<SectorCelestialBodyComponent>(ent, out var body))
        {
            QueueDel(ent);
            return;
        }
        body.Kind = kind;
        RandomizeShaderParams(ent, body, map.MapId);
        Dirty(ent, body);
        _lights.ApplyCelestial(ent, body);
        SyncRadiationSource(ent, body);
        _landmarks.LockToMap(ent);
        placer.Spawned = true;
        Log.Info($"Spawned sector {kind} palette={body.Palette} seed={body.Seed:F2} at {pos} on {ToPrettyString(mapUid)}");
    }

    private void OnBodyMapInit(EntityUid uid, SectorCelestialBodyComponent body, MapInitEvent args)
    {
        if (!body.VisualsInitialized)
        {
            var mapId = Transform(uid).MapID;
            RandomizeShaderParams(uid, body, mapId);
        }
        Dirty(uid, body);
        _lights.ApplyCelestial(uid, body);
        SyncRadiationSource(uid, body);
        _landmarks.LockToMap(uid);
    }

    private void SyncRadiationSource(EntityUid uid, SectorCelestialBodyComponent body)
    {
        if (!TryComp(uid, out RadiationSourceComponent? source)) return;
        var peak = SectorCelestialMobDamage.GetDamageAmount(
            body.MobRadiationDamage,
            SectorCelestialMobDamage.RadiationDamageType);
        SectorCelestialMobDamage.SyncRadiationSource(uid, source, body.RadiationRange, peak);
    }

    private void RandomizeShaderParams(EntityUid uid, SectorCelestialBodyComponent body, MapId mapId)
    {
        body.Seed = 0.01f + _random.NextFloat() * 9.99f;
        var mix = uid.Id * 2654435761u ^ (uint) mapId.GetHashCode() ^ (uint) _random.Next();
        body.Palette = (byte) (mix % (uint) SectorBackgroundPlanetPlacerSystem.PaletteCount);
        body.VisualsInitialized = true;
    }
}
