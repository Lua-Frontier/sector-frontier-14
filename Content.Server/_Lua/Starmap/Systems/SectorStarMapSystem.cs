// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Server._Lua.Sectors;
using Content.Server.GameTicking;
using Content.Server.Station.Components;
using Content.Shared._Lua.Starmap;
using Content.Shared._Lua.Starmap.Components;
using Content.Shared.Lua.CLVar;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;

namespace Content.Server._Lua.Starmap.Systems;

public sealed class SectorStarMapSystem : EntitySystem
{
    [Dependency] private readonly SectorSystem _sectorSystem = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    private float _updateTimer = 0f;

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
        if (!_configurationManager.GetCVar(CLVars.StarmapIncludeSectors))
            return sectorStars;

        var currentPreset = _ticker.CurrentPreset?.ID;

        try
        {
            var dataId = _configurationManager.GetCVar(CLVars.StarmapDataId);
            if (!_prototypes.TryIndex<StarmapDataPrototype>(dataId, out var data))
                return sectorStars;

            foreach (var def in data.Stars)
            {
                if (def.RequiredGamePresets != null && def.RequiredGamePresets.Length > 0)
                {
                    if (currentPreset == null || !def.RequiredGamePresets.Contains(currentPreset))
                        continue;
                }
                else if (!string.IsNullOrWhiteSpace(def.RequiredGamePreset))
                {
                    if (currentPreset != def.RequiredGamePreset)
                        continue;
                }

                MapId mapId;

                if (def.StarType == "frontier")
                {
                    mapId = GetFrontierSectorMapId();
                    if (mapId == MapId.Nullspace) continue;
                }
                else if (def.StarType == "centcom")
                {
                    continue;
                }
                else
                {
                    if (!_sectorSystem.TryGetMapId(def.Id, out var resolved))
                        continue;
                    mapId = resolved;
                }

                if (mapId == MapId.Nullspace) continue;

                var displayName = GetMapEntityName(mapId) ?? def.Name;
                var star = new Star(def.Position, mapId, displayName, Vector2.Zero);
                sectorStars.Add(star);
            }
        }
        catch { }

        return sectorStars;
    }

    private MapId GetFrontierSectorMapId()
    {
        try
        {
            var defaultMap = _ticker.DefaultMap;
            if (_mapManager.MapExists(defaultMap)) return defaultMap;
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
                updatedCount++;
            }
            try { EntityManager.System<StarmapSystem>().InvalidateCache(refreshConsoles: false); }
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
            var dataId = _configurationManager.GetCVar(CLVars.StarmapDataId);
            if (_prototypes.TryIndex<StarmapDataPrototype>(dataId, out var data))
            {
                info.AppendLine($"Stars defined: {data.Stars.Length}");
                info.AppendLine($"Hyperlanes defined: {data.Hyperlanes.Length}");
                foreach (var def in data.Stars)
                {
                    info.AppendLine($"  {def.Name} ({def.Id}): pos={def.Position} type={def.StarType}");
                }
            }
            else
            {
                info.AppendLine($"StarmapData prototype '{dataId}' not found!");
            }
        }
        catch (Exception ex) { info.AppendLine($"Error: {ex.Message}"); }

        info.AppendLine("\nSector MapIds:");
        var frontierMapId = GetFrontierSectorMapId();
        info.AppendLine($"  Frontier: {frontierMapId}");

        var starMapQuery = AllEntityQuery<StarMapComponent>();
        var starMapCount = 0;
        while (starMapQuery.MoveNext(out var uid, out var starMap))
            starMapCount++;
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
