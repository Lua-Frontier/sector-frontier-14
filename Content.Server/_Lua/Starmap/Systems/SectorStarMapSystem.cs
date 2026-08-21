// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Server._Lua.Sectors;
using Content.Server.GameTicking;
using Content.Shared._Lua.Starmap;
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
        Timer.Spawn(2000, () => InvalidateStarmapCache());
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_updateTimer <= 0)
        {
            _updateTimer = 30f;
            InvalidateStarmapCache();
        }
        else
        {
            _updateTimer -= frameTime;
        }
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
            if (!StarmapDataComposer.TryCompose(_prototypes, dataId, out var data))
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

                if (!_sectorSystem.TryGetMapId(def.Id, out var mapId) || mapId == MapId.Nullspace)
                    continue;

                var displayName = GetMapEntityName(mapId) ?? def.Name;
                sectorStars.Add(new Star(def.Position, mapId, displayName, Vector2.Zero));
            }
        }
        catch { }

        return sectorStars;
    }

    public void UpdateAllStarMaps() => InvalidateStarmapCache();

    public void ForceUpdateAllStarMaps() => InvalidateStarmapCache();

    public void OnStationCreated(EntityUid stationUid) => InvalidateStarmapCache();

    public void TriggerStarMapUpdate() => InvalidateStarmapCache();

    private void InvalidateStarmapCache()
    {
        try
        {
            EntityManager.System<StarmapSystem>().InvalidateCache(refreshConsoles: false);
        }
        catch { }
    }

    private string? GetMapEntityName(MapId mapId)
    {
        try
        {
            var mapUid = _mapManager.GetMapEntityId(mapId);
            if (TryComp<MetaDataComponent>(mapUid, out var meta) && !string.IsNullOrWhiteSpace(meta.EntityName))
                return meta.EntityName;
        }
        catch { }

        return null;
    }

    public string GetDiagnosticInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine("=== SectorStarMapSystem Diagnostic Info ===");

        try
        {
            var dataId = _configurationManager.GetCVar(CLVars.StarmapDataId);
            if (StarmapDataComposer.TryCompose(_prototypes, dataId, out var data))
            {
                info.AppendLine($"Stars defined: {data.Stars.Length}");
                info.AppendLine($"Hyperlanes defined: {data.Hyperlanes.Length}");
                foreach (var def in data.Stars)
                    info.AppendLine($"  {def.Name} ({def.Id}): pos={def.Position} type={def.StarType} hub={def.IsHub}");
            }
            else
            {
                info.AppendLine($"StarmapData prototype '{dataId}' not found!");
            }
        }
        catch (Exception ex)
        {
            info.AppendLine($"Error: {ex.Message}");
        }

        info.AppendLine("\nSector MapIds:");
        if (_sectorSystem.TryGetHubMapId(out var hubMap))
            info.AppendLine($"  Hub: {hubMap} ({_sectorSystem.GetHubSectorId()})");
        if (_sectorSystem.TryGetCentComMapId(out var ccMap))
            info.AppendLine($"  CentCom: {ccMap}");

        return info.ToString();
    }
}
