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
using Content.Shared._NF.CCVar;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.EntitySerialization.Systems;

namespace Content.Server.LW.ShipVsShipPOI;

public sealed class ShipVsShipPOISystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MapLoaderSystem _map = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
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

    public void GenerateSectorRedPOIs(MapId mapId, EntityUid mapUid, out List<EntityUid> spawnedPOIs)
    {
        spawnedPOIs = new List<EntityUid>();

        var requiredProtos = new List<PointOfInterestPrototype>();

        var currentPreset = _ticker.CurrentPreset?.ID ?? "ShipVsShip";

        foreach (var location in _proto.EnumeratePrototypes<PointOfInterestPrototype>())
        {
            if (location.SpawnGamePreset.Length > 0 && !location.SpawnGamePreset.Contains(currentPreset))
                continue;

            switch (location.SpawnGroup)
            {
                case "ShipVsShipRedRequired":
                    requiredProtos.Add(location);
                    break;
            }
        }

        GenerateRequireds(mapId, requiredProtos, out var required);

        spawnedPOIs.AddRange(required);
    }

    public void GenerateSectorBluePOIs(MapId mapId, EntityUid mapUid, out List<EntityUid> spawnedPOIs)
    {
        spawnedPOIs = new List<EntityUid>();

        var requiredProtos = new List<PointOfInterestPrototype>();

        var currentPreset = _ticker.CurrentPreset?.ID ?? "ShipVsShip";

        foreach (var location in _proto.EnumeratePrototypes<PointOfInterestPrototype>())
        {
            if (location.SpawnGamePreset.Length > 0 && !location.SpawnGamePreset.Contains(currentPreset))
                continue;

            switch (location.SpawnGroup)
            {
                case "ShipVsShipBlueRequired":
                    requiredProtos.Add(location);
                    break;
            }
        }

        GenerateRequireds(mapId, requiredProtos, out var required);

        spawnedPOIs.AddRange(required);
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
