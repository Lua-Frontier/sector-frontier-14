using System.Linq;
using System.Numerics;
using Content.Server._Lua.Sectors;
using Content.Server.LW.AsteroidSector;
using Content.Server.LW.MercenarySector;
using Content.Server.LW.PirateSector;
using Content.Server.LW.TypanSector;
using Content.Server._NF.SectorServices;
using Content.Server.Station.Components;
using Content.Shared._Lua.FtlPoints;
using Content.Shared._Lua.FtlPoints.Components;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Timing;

namespace Content.Server._Lua.FTLPoints.Systems;

public sealed class SectorStarMapSystem : EntitySystem
{
    [Dependency] private readonly AsteroidSectorSystem _asteroidSector = default!;
    [Dependency] private readonly MercenarySectorSystem _mercenarySector = default!;
    [Dependency] private readonly PirateSectorSystem _pirateSector = default!;
    [Dependency] private readonly TypanSectorSystem _typanSector = default!;
    [Dependency] private readonly SectorServiceSystem _sectorService = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private float _updateTimer = 0f;

    private readonly Dictionary<string, Vector2> _sectorCoordinates = new()
    {
        { "Сектор Фронтир", new Vector2(0f, 0f) },
        { "Поле Астероидов", new Vector2(10.0f, 6.8f) },
        { "Сектор Наёмников", new Vector2(-14.8f, -16.2f) },
        { "Сектор Пиратов", new Vector2(17.6f, -16.5f) },
        { "Сектор Нордфолл", new Vector2(14.9f, 12.0f) }
    };

    public override void Initialize()
    {
        base.Initialize();
        Log.Info("SectorStarMapSystem initialized");
        Timer.Spawn(2000, () =>
        {
            Log.Info("Performing initial StarMap update...");
            UpdateAllStarMaps();
        });
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
        Log.Info("Getting sector stars...");

        try
        {
            var frontierMapId = GetFrontierSectorMapId();
            Log.Info($"Frontier sector MapId: {frontierMapId}");
            if (frontierMapId != MapId.Nullspace || frontierMapId.Equals(new MapId(0)))
            {
                var position = _sectorCoordinates["Сектор Фронтир"];
                var frontierStar = new Star(position, frontierMapId, "Сектор Фронтир", Vector2.Zero);
                sectorStars.Add(frontierStar);
                Log.Info($"Added Frontier Sector at {position} with MapId {frontierMapId} (Main Map)");
            }
            else
            { Log.Warning($"Frontier sector MapId is Nullspace - sector will not be displayed! MapId: {frontierMapId}"); }
            var asteroidMapId = _asteroidSector.GetAsteroidSectorMapId();
            if (asteroidMapId != MapId.Nullspace)
            {
                var position = _sectorCoordinates["Поле Астероидов"];
                sectorStars.Add(new Star(position, asteroidMapId, "Поле Астероидов", Vector2.Zero));
                Log.Info($"Added Asteroid Sector at {position}");
            }
            else
            { Log.Warning("Asteroid sector MapId is Nullspace"); }
            var mercenaryMapId = _mercenarySector.GetMercenarySectorMapId();
            if (mercenaryMapId != MapId.Nullspace)
            {
                var position = _sectorCoordinates["Сектор Наёмников"];
                sectorStars.Add(new Star(position, mercenaryMapId, "Сектор Наёмников", Vector2.Zero));
                Log.Info($"Added Mercenary Sector at {position}");
            }
            else
            { Log.Warning("Mercenary sector MapId is Nullspace"); }
            var pirateMapId = _pirateSector.GetPirateSectorMapId();
            if (pirateMapId != MapId.Nullspace)
            {
                var position = _sectorCoordinates["Сектор Пиратов"];
                sectorStars.Add(new Star(position, pirateMapId, "Сектор Пиратов", Vector2.Zero));
                Log.Info($"Added Pirate Sector at {position}");
            }
            else
            { Log.Warning("Pirate sector MapId is Nullspace"); }
            var typanMapId = _typanSector.GetTypanSectorMapId();
            if (typanMapId != MapId.Nullspace)
            {
                var position = _sectorCoordinates["Сектор Нордфолл"];
                sectorStars.Add(new Star(position, typanMapId, "Сектор Нордфолл", Vector2.Zero));
                Log.Info($"Added Typan Sector at {position}");
            }
            else
            { Log.Warning("Typan sector MapId is Nullspace"); }
        }
        catch (Exception ex)
        { Log.Error($"Error getting sector stars: {ex}"); }
        Log.Info($"Total sector stars: {sectorStars.Count}");
        return sectorStars;
    }

    private MapId GetFrontierSectorMapId()
    {
        try
        {
            Log.Info("Searching for Frontier sector...");
            var stationQuery = AllEntityQuery<StationDataComponent>();
            var stationCount = 0;
            while (stationQuery.MoveNext(out var uid, out var stationData))
            {
                stationCount++;
                Log.Info($"Found station {uid} with {stationData.Grids.Count} grids");
                if (TryComp<MetaDataComponent>(uid, out var meta))
                {
                    var stationName = meta.EntityName ?? "Unknown";
                    Log.Info($"Station {uid} name: {stationName}");
                    if (stationName.Contains("Frontier", StringComparison.OrdinalIgnoreCase) ||
                        stationName.Contains("Фронтир", StringComparison.OrdinalIgnoreCase) ||
                        stationName.Contains("Station", StringComparison.OrdinalIgnoreCase) ||
                        stationName.Contains("Main", StringComparison.OrdinalIgnoreCase))
                    {
                        var transform = Transform(uid);
                        var mapId = transform.MapID;
                        Log.Info($"Found Frontier sector: {uid} at MapId {mapId} (Main Map)");
                        return mapId;
                    }
                }
            }

            Log.Info($"Total stations found: {stationCount}");
            var anyStationQuery = AllEntityQuery<StationDataComponent>();
            if (anyStationQuery.MoveNext(out var anyUid, out _))
            {
                var transform = Transform(anyUid);
                var mapId = transform.MapID;
                Log.Info($"Using fallback station {anyUid} at MapId {mapId}");
                return mapId;
            }
            Log.Warning("No stations found at all!");
        }
        catch (Exception ex)
        { Log.Error($"Error getting Frontier sector MapId: {ex}"); }
        return MapId.Nullspace;
    }

    public void SetSectorPosition(string sectorName, Vector2 position)
    {
        if (_sectorCoordinates.ContainsKey(sectorName))
        {
            _sectorCoordinates[sectorName] = position;
            Log.Info($"Updated {sectorName} position to {position}");
        }
        else
        {
            _sectorCoordinates[sectorName] = position;
            Log.Info($"Added new sector {sectorName} at position {position}");
        }
    }

    public Vector2 GetSectorPosition(string sectorName)
    { return _sectorCoordinates.TryGetValue(sectorName, out var position) ? position : Vector2.Zero; }

    public IEnumerable<string> GetConfiguredSectorNames()
    { return _sectorCoordinates.Keys; }

    public void UpdateAllStarMaps()
    {
        try
        {
            var sectorStars = GetSectorStars();
            Log.Info($"Updating {sectorStars.Count} sector stars in all StarMaps");
            var starMapQuery = AllEntityQuery<StarMapComponent>();
            var updatedCount = 0;
            while (starMapQuery.MoveNext(out var uid, out var starMap))
            {
                UpdateStarMap(starMap, sectorStars);
                Dirty(uid, starMap);
                updatedCount++;
            }
            Log.Info($"Updated {updatedCount} StarMap components");
        }
        catch (Exception ex)
        { Log.Error($"Error updating StarMaps: {ex}"); }
    }

    public void ForceUpdateAllStarMaps()
    {
        Log.Info("Force updating all StarMaps...");
        UpdateAllStarMaps();
    }

    public void OnStationCreated(EntityUid stationUid)
    {
        Log.Info($"Station {stationUid} created, updating StarMaps...");
        UpdateAllStarMaps();
    }

    public string GetDiagnosticInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine("=== SectorStarMapSystem Diagnostic Info ===");
        info.AppendLine($"Configured sectors: {_sectorCoordinates.Count}");
        foreach (var kvp in _sectorCoordinates)
        { info.AppendLine($"  {kvp.Key}: {kvp.Value}"); }
        info.AppendLine("\nSector MapIds:");
        var frontierMapId = GetFrontierSectorMapId();
        if (frontierMapId == MapId.Nullspace)
        { info.AppendLine($"  Сектор Фронтир: {frontierMapId} (NOT FOUND)"); }
        else if (frontierMapId == new MapId(0))
        { info.AppendLine($"  Сектор Фронтир: {frontierMapId} (Main Map - MapId 0)"); }
        else
        { info.AppendLine($"  Сектор Фронтир: {frontierMapId} (Other Map)"); }
        try
        {
            var asteroidMapId = _asteroidSector.GetAsteroidSectorMapId();
            info.AppendLine($"  Поле Астероидов: {asteroidMapId}");
        }
        catch (Exception ex)
        { info.AppendLine($"  Поле Астероидов: ERROR - {ex.Message}"); }
        try
        {
            var mercenaryMapId = _mercenarySector.GetMercenarySectorMapId();
            info.AppendLine($"  Сектор Наёмников: {mercenaryMapId}");
        }
        catch (Exception ex)
        { info.AppendLine($"  Сектор Наёмников: ERROR - {ex.Message}"); }
        try
        {
            var pirateMapId = _pirateSector.GetPirateSectorMapId();
            info.AppendLine($"  Сектор Пиратов: {pirateMapId}");
        }
        catch (Exception ex)
        { info.AppendLine($"  Сектор Пиратов: ERROR - {ex.Message}"); }
        try
        {
            var typanMapId = _typanSector.GetTypanSectorMapId();
            info.AppendLine($"  Сектор Нордфолл: {typanMapId}");
        }
        catch (Exception ex)
        { info.AppendLine($"  Сектор Нордфолл: ERROR - {ex.Message}"); }
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
            var removedCount = 0;
            foreach (var sectorName in _sectorCoordinates.Keys)
            { removedCount += starMap.RemoveStarByName(sectorName) ? 1 : 0; }
            foreach (var star in sectorStars)
            { starMap.AddStar(star); }
            Log.Info($"Updated StarMap: removed {removedCount} old stars, added {sectorStars.Count} new stars");
        }
        catch (Exception ex)
        { Log.Error($"Error updating StarMap: {ex}"); }
    }

    public void TriggerStarMapUpdate()
    {
        Log.Info("Manual StarMap update triggered");
        UpdateAllStarMaps();
    }
}
