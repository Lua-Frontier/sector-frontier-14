// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Server._Lua.Sectors;
using Content.Server._Lua.Starmap.Systems;
using Content.Server.Backmen.Arrivals;
using Content.Server.Shuttles.Components;
using Content.Server.GameTicking;
using Content.Shared._Lua.Starmap;
using Content.Shared._Lua.Starmap.Components;
using Content.Shared.Backmen.Arrivals;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Timing;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Linq;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleConsoleSystem
{
    [Dependency] private readonly StarmapSystem _starmap = default!; // Lua
    [Dependency] private readonly SectorOwnershipSystem _ownership = default!; // Lua
    [Dependency] private readonly SectorSystem _sectors = default!; // Lua
    [Dependency] private readonly CentcommSystem _centcomm = default!; // CentCom
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!; // Lua
    [Dependency] private readonly GameTicker _ticker = default!;

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
        var stars = _starmap.CollectStars();
        if (stars.Count == 0)
        { stars = _starmap.CollectStarsFresh(updateCache: true); }
        var edges = _starmap.GetHyperlanesCached();
        if ((edges == null || edges.Count == 0) && stars.Count > 0)
        { edges = EntityManager.System<StarmapSystem>().GetHyperlanesCached(); }
        bool allowCentComStar = _centcomm.CentComStarUnlocked; // Lua
        if (!allowCentComStar && consoleUid != null)
        {
            try
            {
                if (_containers.TryGetContainer(consoleUid.Value, "disk_slot", out var ccDiskCont) && ccDiskCont.ContainedEntities.Count > 0)
                {
                    var ccDisk = ccDiskCont.ContainedEntities[0];
                    if (TryComp<StarMapCoordinatesDiskComponent>(ccDisk, out var ccDiskComp) && ccDiskComp.AllowFtlToCentCom) allowCentComStar = true;
                }
            }
            catch { }
        }
        if (allowCentComStar && _centcomm.CentComMap != MapId.Nullspace)
        {
            var ccMap = _centcomm.CentComMap;
            if (!stars.Any(s => s.Map == ccMap))
            {
                try
                {
                    if (_prototypes.TryIndex<StarmapConfigPrototype>("StarmapConfig", out var stCfg))
                    {
                        foreach (var sp in stCfg.SpecialSectors)
                        {
                            if (string.Equals(sp.Id, "CentCom", StringComparison.Ordinal))
                            {
                                var ccPos = sp.Position;
                                stars.Add(new Star(ccPos, ccMap, "Central Command", ccPos));
                                break;
                            }
                        }
                    }
                }
                catch { }
            }
        }
        if (currentMap != MapId.Nullspace)
        {
            Star? pivot = null;
            foreach (var s in stars)
            { if (s.Map == currentMap) { pivot = s; break; } }
            if (pivot.HasValue)
            {
                var offset = pivot.Value.Position;
                for (var i = 0; i < stars.Count; i++)
                { var s = stars[i]; stars[i] = new Star(s.Position - offset, s.Map, s.Name, s.GlobalPosition - offset); }
            }
        }
        var visibleSectorMaps = new List<MapId>();
        var sectorIdByMap = new Dictionary<MapId, string>();
        if (consoleUid != null)
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
                                if (string.IsNullOrWhiteSpace(sid)) continue;
                                MapId mapId;
                                if (sid == "FrontierSector")
                                { mapId = _ticker.DefaultMap; }
                                else if (_sectors.TryGetMapId(sid, out var resolved))
                                { mapId = resolved; }
                                else
                                { continue; }
                                {
                                    var star = stars.FirstOrDefault(s => s.Map == mapId);
                                    if (!string.IsNullOrEmpty(star.Name) && !visibleSectorMaps.Contains(mapId)) visibleSectorMaps.Add(mapId);
                                    if (!sectorIdByMap.ContainsKey(mapId)) sectorIdByMap[mapId] = sid;
                                }
                            }
                        }
                        if (diskComp.AllowFtlToCentCom && _centcomm.CentComMap != MapId.Nullspace)
                        {
                            var ccMap = _centcomm.CentComMap;
                            if (!visibleSectorMaps.Contains(ccMap)) visibleSectorMaps.Add(ccMap);
                            if (!sectorIdByMap.ContainsKey(ccMap)) sectorIdByMap[ccMap] = "CentCom";
                        }
                    }
                }
            }
            catch { }
        }
        if (_centcomm.CentComStarUnlocked && _centcomm.CentComMap != MapId.Nullspace)
        {
            var ccMap = _centcomm.CentComMap;
            if (!visibleSectorMaps.Contains(ccMap)) visibleSectorMaps.Add(ccMap);
            if (!sectorIdByMap.ContainsKey(ccMap)) sectorIdByMap[ccMap] = "CentCom";
        }
        try
        {
            var frontierMap = _ticker.DefaultMap;
            if (!sectorIdByMap.ContainsKey(frontierMap)) sectorIdByMap[frontierMap] = "FrontierSector";
        }
        catch { }
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
                    if (ms.FTLTime.Start != default && ms.FTLTime.End > ms.FTLTime.Start) cooldownTotal = (float)(ms.FTLTime.End - ms.FTLTime.Start).TotalSeconds;
                }
            }
            catch
            {
                ftlState = FTLState.Available;
                ftlTime = default;
            }
        }
        foreach (var sid in new[] { "AsteroidSectorDefault", "MercenarySector", "PirateSector", "TypanSector" })
        { if (_sectors.TryGetMapId(sid, out var mid) && !sectorIdByMap.ContainsKey(mid)) sectorIdByMap[mid] = sid; }
        if (allowCentComStar && _centcomm.CentComMap != MapId.Nullspace)
        {
            var frontierIdx = stars.FindIndex(s => s.Map == _ticker.DefaultMap);
            var ccIdx = stars.FindIndex(s => s.Map == _centcomm.CentComMap);
            if (frontierIdx >= 0 && ccIdx >= 0)
            {
                var a = Math.Min(frontierIdx, ccIdx);
                var b = Math.Max(frontierIdx, ccIdx);
                var hasEdge = edges != null && edges.Any(e => (e.A == a && e.B == b) || (e.A == b && e.B == a));
                if (!hasEdge)
                {
                    var edges2 = edges != null ? new List<HyperlaneEdge>(edges) : new List<HyperlaneEdge>();
                    edges2.Add(new HyperlaneEdge(a, b));
                    edges = edges2;
                }
            }
        }
        var ownerByMap = new Dictionary<MapId, string>();
        var colorOverrides = new Dictionary<MapId, string>();
        try
        {
            foreach (var kv in _ownership.GetOwnerByMap())
            { ownerByMap[kv.Key] = kv.Value; }
            foreach (var kv in _ownership.GetSectorColorOverridesHex())
            { colorOverrides[kv.Key] = kv.Value; }
        }
        catch { }
        return new StarmapConsoleBoundUserInterfaceState(stars, 100f, edges, cooldown, cooldownTotal, ftlState, ftlTime, visibleSectorMaps, sectorIdByMap, ownerByMap, colorOverrides);
    }

    private void OnWarpToStarMessage(EntityUid uid, ShuttleConsoleComponent component, WarpToStarMessage args) // Lua
    { try { EntityManager.System<SimpleStarmapSystem>().WarpToStar(uid, args.Star); } catch { } }
}
