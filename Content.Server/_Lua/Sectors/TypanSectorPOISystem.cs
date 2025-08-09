// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using System.Linq;
using System.Numerics;
using Content.Server._NF.GameRule;
using Content.Server._NF.Station.Systems;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Server.Station.Systems;
using Content.Server.Shuttles.Systems;
using Content.Shared._NF.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Prototypes;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using Robust.Shared.EntitySerialization.Systems;
using Content.Shared.Lua.CLVar;

namespace Content.Server.LW.TypanSectorPOI;

public sealed class TypanSectorPOISystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MapLoaderSystem _map = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly StationRenameWarpsSystems _renameWarps = default!;

    private List<Vector2> _stationCoords = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _stationCoords.Clear();
    }

    private void AddStationCoordsToSet(Vector2 coords)
    {
        _stationCoords.Add(coords);
    }

    public void GenerateSectorPOIs(MapId mapId, EntityUid mapUid, out List<EntityUid> spawnedPOIs)
    {
        spawnedPOIs = new List<EntityUid>();

        var depotProtos = new List<PointOfInterestPrototype>();
        var marketProtos = new List<PointOfInterestPrototype>();
        var requiredProtos = new List<PointOfInterestPrototype>();

        var currentPreset = _ticker.CurrentPreset?.ID ?? "NFPirates";

        foreach (var location in _proto.EnumeratePrototypes<PointOfInterestPrototype>())
        {
            if (location.SpawnGamePreset.Length > 0 && !location.SpawnGamePreset.Contains(currentPreset))
                continue;

            switch (location.SpawnGroup)
            {
                case "TypanCargoDepot":
                    depotProtos.Add(location);
                    break;
                case "TypanMarketStation":
                    marketProtos.Add(location);
                    break;
                case "TypanRequired":
                    requiredProtos.Add(location);
                    break;
            }
        }

        GenerateDepots(mapId, depotProtos, out var depots);
        GenerateMarkets(mapId, marketProtos, out var markets);
        GenerateRequireds(mapId, requiredProtos, out var required);

        spawnedPOIs.AddRange(depots);
        spawnedPOIs.AddRange(markets);
        spawnedPOIs.AddRange(required);
    }

    private void GenerateDepots(MapId mapId, List<PointOfInterestPrototype> prototypes, out List<EntityUid> grids)
    {
        grids = new List<EntityUid>();
        var depotCount = _cfg.GetCVar(CLVars.TypanCargoDepots);
        if (prototypes.Count == 0 || depotCount <= 0)
            return;

        var rotation = 2 * Math.PI / depotCount;
        var rotationOffset = _random.NextAngle() / depotCount;

        for (int i = 0; i < depotCount && prototypes.Count > 0; i++)
        {
            var proto = _random.Pick(prototypes);
            Vector2i offset = new Vector2i(_random.Next(proto.MinimumDistance, proto.MaximumDistance), 0);
            offset = offset.Rotate(rotationOffset);
            rotationOffset += rotation;

            string overrideName = proto.Name + (i < 26 ? $" {(char)('A' + i)}" : $" {i + 1}");

            if (TrySpawnPoiGrid(mapId, proto, offset, out var gridUid, overrideName) && gridUid.HasValue)
                grids.Add(gridUid.Value);
        }
    }

    private void GenerateMarkets(MapId mapId, List<PointOfInterestPrototype> prototypes, out List<EntityUid> grids)
    {
        grids = new List<EntityUid>();
        var marketCount = _cfg.GetCVar(CLVars.TypanMarketStations);
        if (prototypes.Count == 0 || marketCount <= 0)
            return;

        _random.Shuffle(prototypes);
        int spawned = 0;

        foreach (var proto in prototypes)
        {
            if (spawned >= marketCount)
                break;

            var offset = GetRandomPOICoord(proto.MinimumDistance, proto.MaximumDistance);
            if (TrySpawnPoiGrid(mapId, proto, offset, out var gridUid) && gridUid.HasValue)
            {
                grids.Add(gridUid.Value);
                spawned++;
            }
        }
    }

    private void GenerateRequireds(MapId mapId, List<PointOfInterestPrototype> prototypes, out List<EntityUid> grids)
    {
        grids = new List<EntityUid>();
        foreach (var proto in prototypes)
        {
            var offset = GetRandomPOICoord(proto.MinimumDistance, proto.MaximumDistance);
            if (TrySpawnPoiGrid(mapId, proto, offset, out var gridUid) && gridUid.HasValue)
                grids.Add(gridUid.Value);
        }
    }

    private bool TrySpawnPoiGrid(MapId mapUid, PointOfInterestPrototype proto, Vector2 offset, out EntityUid? gridUid, string? overrideName = null)
    {
        gridUid = null;
        if (!_map.TryLoadGrid(mapUid, proto.GridPath, out var loadedGrid, offset: offset, rot: _random.NextAngle()))
            return false;
        gridUid = loadedGrid.Value;
        List<EntityUid> gridList = [loadedGrid.Value];

        string stationName = string.IsNullOrEmpty(overrideName) ? proto.Name : overrideName;

        EntityUid? stationUid = null;
        if (_proto.TryIndex<GameMapPrototype>(proto.ID, out var stationProto))
            stationUid = _station.InitializeNewStation(stationProto.Stations[proto.ID], gridList, stationName);

        var meta = EnsureComp<MetaDataComponent>(loadedGrid.Value);
        _meta.SetEntityName(loadedGrid.Value, stationName, meta);

        EntityManager.AddComponents(loadedGrid.Value, proto.AddComponents);

        // Rename warp points after set up if needed
        if (proto.NameWarp)
        {
            bool? hideWarp = proto.HideWarp ? true : null;
            if (stationUid != null)
                _renameWarps.SyncWarpPointsToStation(stationUid.Value, forceAdminOnly: hideWarp);
            else
                _renameWarps.SyncWarpPointsToGrids(gridList, forceAdminOnly: hideWarp);
        }

        return true;
    }

    private Vector2 GetRandomPOICoord(float minRange, float maxRange)
    {
        int numRetries = _cfg.GetCVar(NFCCVars.POIPlacementRetries);

        Vector2 coords = _random.NextVector2(minRange, maxRange);
        for (int i = 0; i < numRetries; i++)
        {
            bool valid = true;
            foreach (var station in _stationCoords)

            if (valid)
                break;

            coords = _random.NextVector2(minRange, maxRange);
        }

        AddStationCoordsToSet(coords);
        return coords;
    }
}
