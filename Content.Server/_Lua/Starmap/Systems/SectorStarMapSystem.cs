// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Server._Lua.Sectors;
using Content.Server.Station.Components;
using Content.Shared._Lua.Starmap;
using Content.Shared._Lua.Starmap.Components;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;
using Content.Shared._Lua.Sectors;

namespace Content.Server._Lua.Starmap.Systems;

public sealed class SectorStarMapSystem : EntitySystem
{
    [Dependency] private readonly SectorSystem _sectorSystem = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    private float _updateTimer = 0f;

    private bool TryGetConfiguredPosition(string sectorProtoId, out Vector2 position)
    {
        position = default;
        try
        { if (_prototypes.TryIndex<SectorSystemPrototype>(sectorProtoId, out var proto) && proto.StarmapPosition != null) { position = proto.StarmapPosition.Value; return true; } }
        catch { }
        return false;
    }

    private bool TryGetSpecialPosition(string id, out Vector2 position)
    {
        position = default;
        try
        {
            if (_prototypes.TryIndex<StarmapConfigPrototype>("StarmapConfig", out var cfg))
            { foreach (var sp in cfg.SpecialSectors) { if (string.Equals(sp.Id, id, StringComparison.Ordinal)) { position = sp.Position; return true; } } }
        }
        catch { }
        return false;
    }

    public override void Initialize()
    {
        base.Initialize();
        Timer.Spawn(2000, () => { UpdateAllStarMaps(); });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_updateTimer <= 0)
        {
            _updateTimer = 30f;
            UpdateAllStarMaps();
        }
        else
        { _updateTimer -= frameTime; }
    }

    public List<Star> GetSectorStars()
    {
        var sectorStars = new List<Star>();
        try
        {
            var frontierMapId = GetFrontierSectorMapId();
            if (frontierMapId != MapId.Nullspace || frontierMapId.Equals(new MapId(0)))
            {
                if (TryGetSpecialPosition("FrontierSector", out var position))
                {
                    var frontierName = GetMapEntityName(frontierMapId) ?? "Frontier Sector";
                    var frontierStar = new Star(position, frontierMapId, frontierName, Vector2.Zero);
                    sectorStars.Add(frontierStar);
                }
            }
            var asteroidMapId = _sectorSystem.TryGetMapId("AsteroidSectorDefault", out var asteroidMap) ? asteroidMap : MapId.Nullspace;
            if (asteroidMapId != MapId.Nullspace)
            {
                if (TryGetConfiguredPosition("AsteroidSectorDefault", out var position))
                {
                    var display = GetMapEntityName(asteroidMapId) ?? "Asteroid Field";
                    var star = new Star(position, asteroidMapId, display, Vector2.Zero);
                    sectorStars.Add(star);
                }
            }
            var mercenaryMapId = _sectorSystem.TryGetMapId("MercenarySector", out var mercenaryMap) ? mercenaryMap : MapId.Nullspace;
            if (mercenaryMapId != MapId.Nullspace)
            {
                if (TryGetConfiguredPosition("MercenarySector", out var position))
                {
                    var display = GetMapEntityName(mercenaryMapId) ?? "Mercenary Sector";
                    var star = new Star(position, mercenaryMapId, display, Vector2.Zero);
                    sectorStars.Add(star);
                }
            }
            var pirateMapId = _sectorSystem.TryGetMapId("PirateSector", out var pirateMap) ? pirateMap : MapId.Nullspace;
            if (pirateMapId != MapId.Nullspace)
            {
                if (TryGetConfiguredPosition("PirateSector", out var position))
                {
                    var display = GetMapEntityName(pirateMapId) ?? "Pirate Sector";
                    var star = new Star(position, pirateMapId, display, Vector2.Zero);
                    sectorStars.Add(star);
                }
            }
            var typanMapId = _sectorSystem.TryGetMapId("TypanSector", out var typanMap) ? typanMap : MapId.Nullspace;
            if (typanMapId != MapId.Nullspace)
            {
                if (TryGetConfiguredPosition("TypanSector", out var position))
                {
                    var display = GetMapEntityName(typanMapId) ?? "Nordfall Sector";
                    var star = new Star(position, typanMapId, display, Vector2.Zero);
                    sectorStars.Add(star);
                }
            }
        }
        catch { }
        return sectorStars;
    }

    private MapId GetFrontierSectorMapId()
    {
        try
        {
            var stationQuery = AllEntityQuery<StationDataComponent>();
            var stationCount = 0;
            while (stationQuery.MoveNext(out var uid, out var stationData))
            {
                stationCount++;
                if (TryComp<MetaDataComponent>(uid, out var meta))
                {
                    var stationName = meta.EntityName ?? "Unknown";
                    if (stationName.Contains("Frontier", StringComparison.OrdinalIgnoreCase) ||
                        stationName.Contains("Station", StringComparison.OrdinalIgnoreCase) ||
                        stationName.Contains("Main", StringComparison.OrdinalIgnoreCase))
                    {
                        var transform = Transform(uid);
                        var mapId = transform.MapID;
                        return mapId;
                    }
                }
            }
            var anyStationQuery = AllEntityQuery<StationDataComponent>();
            if (anyStationQuery.MoveNext(out var anyUid, out _))
            {
                var transform = Transform(anyUid);
                var mapId = transform.MapID;
                return mapId;
            }
        }
        catch { }
        return MapId.Nullspace;
    }

    public void UpdateAllStarMaps()
    {
        try
        {
            var sectorStars = GetSectorStars();
            var starMapQuery = AllEntityQuery<StarMapComponent>();
            var updatedCount = 0;
            while (starMapQuery.MoveNext(out var uid, out var starMap))
            {
                UpdateStarMap(starMap, sectorStars);
                Dirty(uid, starMap);
                updatedCount++;
            }
            try { EntityManager.System<StarmapSystem>().InvalidateCache(); }
            catch { }
        }
        catch { }
    }

    private string? GetMapEntityName(MapId mapId)
    {
        try
        {
            var mapUid = _mapManager.GetMapEntityId(mapId);
            if (TryComp<MetaDataComponent>(mapUid, out var meta) && !string.IsNullOrWhiteSpace(meta.EntityName)) return meta.EntityName;
        }
        catch { }
        return null;
    }

    public void ForceUpdateAllStarMaps()
    { UpdateAllStarMaps(); }

    public void OnStationCreated(EntityUid stationUid)
    { UpdateAllStarMaps(); }

    public string GetDiagnosticInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine("=== SectorStarMapSystem Diagnostic Info ===");
        try
        {
            var listed = 0;
            foreach (var proto in _prototypes.EnumeratePrototypes<SectorSystemPrototype>())
            {
                if (proto.StarmapPosition == null) continue;
                info.AppendLine($"  {proto.Name} ({proto.ID}): {proto.StarmapPosition.Value}");
                listed++;
            }
            info.AppendLine($"Configured sectors: {listed}");
        }
        catch { info.AppendLine("Configured sectors: (error enumerating)"); }
        info.AppendLine("\nSector MapIds:");
        var frontierMapId = GetFrontierSectorMapId();
        if (frontierMapId == MapId.Nullspace)
        { info.AppendLine($"  Frontier Sector: {frontierMapId} (NOT FOUND)"); }
        else if (frontierMapId == new MapId(0))
        { info.AppendLine($"  Frontier Sector: {frontierMapId} (Main Map - MapId 0)"); }
        else
        { info.AppendLine($"  Frontier Sector: {frontierMapId} (Other Map)"); }
        try
        {
            var asteroidMapId = _sectorSystem.TryGetMapId("AsteroidSectorDefault", out var asteroidMap) ? asteroidMap : MapId.Nullspace;
            info.AppendLine($"  Asteroid Field: {asteroidMapId}");
        }
        catch (Exception ex)
        { info.AppendLine($"  Asteroid Field: ERROR - {ex.Message}"); }
        try
        {
            var mercenaryMapId = _sectorSystem.TryGetMapId("MercenarySector", out var mercenaryMap) ? mercenaryMap : MapId.Nullspace;
            info.AppendLine($"  Mercenary Sector: {mercenaryMapId}");
        }
        catch (Exception ex)
        { info.AppendLine($"  Mercenary Sector: ERROR - {ex.Message}"); }
        try
        {
            var pirateMapId = _sectorSystem.TryGetMapId("PirateSector", out var pirateMap) ? pirateMap : MapId.Nullspace;
            info.AppendLine($"  Pirate Sector: {pirateMapId}");
        }
        catch (Exception ex)
        { info.AppendLine($"  Pirate Sector: ERROR - {ex.Message}"); }
        try
        {
            var typanMapId = _sectorSystem.TryGetMapId("TypanSector", out var typanMap) ? typanMap : MapId.Nullspace;
            info.AppendLine($"  Nordfall Sector: {typanMapId}");
        }
        catch (Exception ex)
        { info.AppendLine($"  Nordfall Sector: ERROR - {ex.Message}"); }
        var starMapQuery = AllEntityQuery<StarMapComponent>();
        var starMapCount = 0;
        while (starMapQuery.MoveNext(out var uid, out var starMap))
        { starMapCount++; }
        info.AppendLine($"\nStarMap components found: {starMapCount}");
        return info.ToString();
    }

    private void UpdateStarMap(StarMapComponent starMap, List<Star> sectorStars)
    {
        try
        {
            var names = new HashSet<string>();
            foreach (var st in sectorStars) { if (!string.IsNullOrEmpty(st.Name)) names.Add(st.Name); }
            foreach (var name in names) { starMap.RemoveStarByName(name); }
            foreach (var star in sectorStars) { starMap.AddStar(star); }
        }
        catch { }
    }

    public void TriggerStarMapUpdate()
    { UpdateAllStarMaps(); }
}
