// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Server._Lua.Company;
using Content.Server._Lua.Sectors;
using Content.Server._Lua.Starmap.Systems;
using Content.Server.Shuttles.Components;
using Content.Shared.Lua.CLVar;
using Content.Shared._Lua.Starmap;
using Content.Shared._Lua.Starmap.Components;
using Content.Shared._Mono.Company;
using Content.Shared.Backmen.Arrivals;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Timing;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleConsoleSystem
{
    [Dependency] private readonly StarmapSystem _starmap = default!; // Lua
    [Dependency] private readonly FactionOwnedStationSystem _factionOwnedStations = default!; // Lua
    [Dependency] private readonly SectorSystem _sectors = default!; // Lua
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!; // Lua
    [Dependency] private readonly FactionWarSystem _factionWar = default!; // Lua

    private void OnConsoleDiskInserted(EntityUid uid, ShuttleConsoleComponent component, EntInsertedIntoContainerMessage args) // Lua
    {
        if (args.Container.ID != "disk_slot") return;
        try
        {
            var xform = Transform(uid);
            var grid = xform.GridUid;
            if (grid != null && TryComp<StarMapCoordinatesDiskComponent>(args.Entity, out var diskComp))
            { if (diskComp.AllowFtlToCentCom) { EnsureComp<AllowFtlToCentComComponent>(grid.Value); } }
        }
        catch { }
        DockingInterfaceState? dockState = null;
        UpdateState(uid, ref dockState);
    }

    private void OnConsoleDiskRemoved(EntityUid uid, ShuttleConsoleComponent component, EntRemovedFromContainerMessage args) // Lua
    {
        if (args.Container.ID != "disk_slot") return;
        try
        {
            var xform = Transform(uid);
            var grid = xform.GridUid;
            if (grid != null)
            { if (HasComp<AllowFtlToCentComComponent>(grid.Value)) { RemCompDeferred<AllowFtlToCentComComponent>(grid.Value); } }
        }
        catch { }
        DockingInterfaceState? dockState = null;
        UpdateState(uid, ref dockState);
    }

    private StarmapConsoleBoundUserInterfaceState GetStarMapState(MapId currentMap, EntityUid? shuttleGridUid, EntityUid? consoleUid = null) // Lua
    {
        var viewer = ResolveStarMapViewer(consoleUid);
        var viewerCompany = ResolveStarMapViewerCompany(viewer);
        var viewerLearned = ResolveStarMapViewerLearned(viewer);
        var sectorsGloballyUnlocked = _factionWar.AreFactionSectorsUnlocked();
        var starmapData = TryGetStarmapData();

        var stars = _starmap.CollectStars();
        if (stars.Count == 0)
            stars = _starmap.CollectStarsFresh(updateCache: true);
        var edges = _starmap.GetHyperlanesCached();
        if ((edges == null || edges.Count == 0) && stars.Count > 0)
            edges = EntityManager.System<StarmapSystem>().GetHyperlanesCached();

        if (currentMap != MapId.Nullspace)
        {
            Star? pivot = null;
            foreach (var s in stars)
            {
                if (s.Map == currentMap)
                {
                    pivot = s;
                    break;
                }
            }

            if (pivot.HasValue)
            {
                var offset = pivot.Value.Position;
                for (var i = 0; i < stars.Count; i++)
                {
                    var s = stars[i];
                    stars[i] = new Star(s.Position - offset, s.Map, s.Name, s.GlobalPosition - offset, s.CanWarp);
                }
            }
        }

        var visibleSectorMaps = new List<MapId>();
        var sectorIdByMap = new Dictionary<MapId, string>();
        if (starmapData != null)
        {
            foreach (var def in starmapData.Stars)
            {
                if (!_sectors.TryGetMapId(def.Id, out var mapId) || mapId == MapId.Nullspace)
                    continue;

                if (!sectorIdByMap.ContainsKey(mapId))
                    sectorIdByMap[mapId] = def.Id;
            }
        }

        if (currentMap != MapId.Nullspace && !visibleSectorMaps.Contains(currentMap))
            visibleSectorMaps.Add(currentMap);

        if (starmapData != null)
        {
            foreach (var def in starmapData.Stars)
            {
                if (!SectorVisibility.IsSectorVisible(def, viewerCompany, sectorsGloballyUnlocked, viewerLearned))
                    continue;

                if (!TryResolveStarMapId(def, stars, out var mapId) || mapId == MapId.Nullspace)
                    continue;

                if (!visibleSectorMaps.Contains(mapId))
                    visibleSectorMaps.Add(mapId);

                if (!string.Equals(def.StarType, "decorative", StringComparison.OrdinalIgnoreCase)
                    && !sectorIdByMap.ContainsKey(mapId))
                    sectorIdByMap[mapId] = def.Id;
            }
        }

        if (consoleUid != null && _configurationManager.GetCVar(CLVars.StarmapRequireSectorDisks))
        {
            try
            {
                if (_containers.TryGetContainer(consoleUid.Value, "disk_slot", out var diskCont) && diskCont.ContainedEntities.Count > 0)
                {
                    var disk = diskCont.ContainedEntities[0];
                    if (TryComp<StarMapCoordinatesDiskComponent>(disk, out var diskComp))
                    {
                        if (diskComp.AllowedSectorIds.Count > 0)
                        {
                            foreach (var sid in diskComp.AllowedSectorIds)
                            {
                                if (string.IsNullOrWhiteSpace(sid))
                                    continue;
                                if (starmapData != null &&
                                    !SectorVisibility.IsSectorVisible(starmapData, sid, viewerCompany, sectorsGloballyUnlocked, viewerLearned))
                                    continue;
                                if (!_sectors.TryGetMapId(sid, out var mapId) || mapId == MapId.Nullspace)
                                    continue;

                                if (!visibleSectorMaps.Contains(mapId))
                                    visibleSectorMaps.Add(mapId);
                                if (!sectorIdByMap.ContainsKey(mapId))
                                    sectorIdByMap[mapId] = sid;
                            }
                        }

                        if (diskComp.AllowFtlToCentCom &&
                            _sectors.TryGetCentComMapId(out var ccMap) &&
                            ccMap != MapId.Nullspace &&
                            starmapData != null &&
                            _sectors.TryGetCentComSectorId(out var ccSectorId) &&
                            SectorVisibility.IsSectorVisible(starmapData, ccSectorId, viewerCompany, sectorsGloballyUnlocked, viewerLearned))
                        {
                            if (!visibleSectorMaps.Contains(ccMap))
                                visibleSectorMaps.Add(ccMap);
                            if (!sectorIdByMap.ContainsKey(ccMap))
                                sectorIdByMap[ccMap] = ccSectorId;
                        }
                    }
                }
            }
            catch { }
        }

        float cooldown = 0f;
        float cooldownTotal = 0f;
        var ftlState = FTLState.Invalid;
        StartEndTime ftlTime = default;
        if (shuttleGridUid != null)
        {
            try
            {
                var ms = GetMapState(shuttleGridUid.Value);
                ftlState = ms.FTLState;
                ftlTime = ms.FTLTime;
                if (ftlState == FTLState.Cooldown)
                {
                    var now = IoCManager.Resolve<IGameTiming>().CurTime;
                    cooldown = (float)Math.Max(0, (ms.FTLTime.End - now).TotalSeconds);
                    if (ms.FTLTime.Start != default && ms.FTLTime.End > ms.FTLTime.Start)
                        cooldownTotal = (float)(ms.FTLTime.End - ms.FTLTime.Start).TotalSeconds;
                }
            }
            catch
            {
                ftlState = FTLState.Available;
                ftlTime = default;
            }
        }

        var ownerByMap = new Dictionary<MapId, string>();
        _factionOwnedStations.BuildMapOwnership(ownerByMap);
        return new StarmapConsoleBoundUserInterfaceState(stars, 100f, edges, null, cooldown, cooldownTotal, ftlState, ftlTime, visibleSectorMaps, sectorIdByMap, ownerByMap, new Dictionary<MapId, string>(), sectorsGloballyUnlocked);
    }

    private EntityUid? ResolveStarMapViewer(EntityUid? consoleUid)
    {
        EntityUid? viewer = null;
        if (consoleUid != null && _starMapViewers.TryGetValue(consoleUid.Value, out var stored) && Exists(stored))
            viewer = stored;
        if (viewer == null && consoleUid != null)
        {
            var query = EntityQueryEnumerator<PilotComponent>();
            while (query.MoveNext(out var pilotUid, out var pilot))
            {
                if (pilot.Console == consoleUid)
                {
                    viewer = pilotUid;
                    break;
                }
            }
        }

        if (viewer == null && consoleUid != null)
        {
            foreach (var actor in _ui.GetActors(consoleUid.Value, ShuttleConsoleUiKey.Key))
            {
                viewer = actor;
                break;
            }
        }

        return viewer;
    }

    private string ResolveStarMapViewerCompany(EntityUid? viewer)
    {
        if (viewer != null &&
            TryComp<CompanyComponent>(viewer.Value, out var company) &&
            !string.IsNullOrWhiteSpace(company.CompanyName))
            return company.CompanyName;

        return SectorVisibility.NoneCompany;
    }

    private IReadOnlyCollection<string>? ResolveStarMapViewerLearned(EntityUid? viewer)
    {
        if (viewer != null && TryComp<KnownSectorsComponent>(viewer.Value, out var known))
            return known.LearnedSectorIds;

        return null;
    }

    private ComposedStarmapData? TryGetStarmapData()
    {
        try
        {
            var dataId = _configurationManager.GetCVar(CLVars.StarmapDataId);
            if (StarmapDataComposer.TryCompose(_prototypes, dataId, out var data))
                return data;
        }
        catch { }

        return null;
    }

    private bool TryResolveStarMapId(StarDefinition def, List<Star> stars, out MapId mapId)
    {
        if (_sectors.TryGetMapId(def.Id, out mapId) && mapId != MapId.Nullspace)
            return true;

        foreach (var star in stars)
        {
            if (!string.Equals(star.Name, def.Name, StringComparison.Ordinal))
                continue;
            mapId = star.Map;
            return mapId != MapId.Nullspace;
        }

        mapId = MapId.Nullspace;
        return false;
    }

    private void OnWarpToStarMessage(EntityUid uid, ShuttleConsoleComponent component, WarpToStarMessage args) // Lua
    {
        try
        {
            EntityManager.System<SimpleStarmapSystem>().WarpToStar(uid, args.Star, args.Actor);
        }
        catch { }
    }
}
