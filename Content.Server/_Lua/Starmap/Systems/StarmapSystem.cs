// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Server.Shuttles.Systems;
using Content.Server._Lua.Sectors;
using Content.Shared._Lua.Starmap;
using Content.Shared._Lua.Starmap.Components;
using Content.Shared.Examine;
using Robust.Shared.Timing;
using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Map;
using System.Linq;

namespace Content.Server._Lua.Starmap.Systems;

public sealed partial class StarmapSystem : SharedStarmapSystem
{
    [Dependency] private readonly SectorStarMapSystem _sectorStarMap = default!;
    [Dependency] private readonly ShuttleConsoleSystem _shuttleConsole = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SectorSystem _sectors = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BluespaceDriveComponent, ExaminedEvent>(OnDriveExamineEvent);
        TryLoadConfig();
        SubscribeLocalEvent<MapRemovedEvent>(OnMapRemoved);
    }

    private float _hyperlaneMaxDistance = 1200f;
    private int _hyperlaneNeighbors = 3;

    private readonly HashSet<(MapId a, MapId b)> _manualHyperlanes = new();
    private readonly HashSet<(MapId a, MapId b)> _blockedHyperlanes = new();

    private static (MapId a, MapId b) NormalizeMapPair(MapId a, MapId b)
    { return ((int) a <= (int) b) ? (a, b) : (b, a); }

    public void ClearHyperlaneOverrides(bool invalidateCache = true)
    {
        _manualHyperlanes.Clear();
        _blockedHyperlanes.Clear();
        if (invalidateCache) InvalidateCache();
    }

    public bool TryAddHyperlane(MapId mapA, MapId mapB)
    {
        if (mapA == mapB) return false;

        var stars = CollectStars();
        var idxA = stars.FindIndex(s => s.Map == mapA);
        var idxB = stars.FindIndex(s => s.Map == mapB);
        if (idxA < 0 || idxB < 0) return false;
        var pair = NormalizeMapPair(mapA, mapB);
        var added = _manualHyperlanes.Add(pair);
        if (added)
        {
            _blockedHyperlanes.Remove(pair);
            InvalidateCache();
        }
        return added;
    }

    public bool TryBlockHyperlane(MapId mapA, MapId mapB)
    {
        if (mapA == mapB) return false;
        var stars = CollectStars();
        var idxA = stars.FindIndex(s => s.Map == mapA);
        var idxB = stars.FindIndex(s => s.Map == mapB);
        if (idxA < 0 || idxB < 0) return false;
        var edges = GetHyperlanesCached();
        var exists = edges.Any(e => (e.A == idxA && e.B == idxB) || (e.A == idxB && e.B == idxA));
        var pair = NormalizeMapPair(mapA, mapB);
        var removedManual = _manualHyperlanes.Remove(pair);
        var addedBlocked = _blockedHyperlanes.Add(pair);
        if (removedManual || addedBlocked) InvalidateCache();
        return exists;
    }

    private void TryLoadConfig()
    {
        try
        {
            if (_prototypes.TryIndex<StarmapConfigPrototype>("StarmapConfig", out var cfg))
            {
                _hyperlaneMaxDistance = cfg.HyperlaneMaxDistance;
                _hyperlaneNeighbors = Math.Max(1, cfg.HyperlaneNeighbors);
            }
        }
        catch { }
    }

    private List<Star> GetAllStars()
    {
        var stars = new List<Star>();
        var seenMaps = new HashSet<MapId>();
        var starMapQuery = AllEntityQuery<StarMapComponent>();
        while (starMapQuery.MoveNext(out var uid, out var starMap))
        {
            foreach (var s in starMap.StarMap)
            {
                if (_mapManager.MapExists(s.Map) && seenMaps.Add(s.Map))
                    stars.Add(s);
            }
        }
        try
        {
            if (_sectorStarMap != null)
            {
                var sectorStars = _sectorStarMap.GetSectorStars();
                foreach (var s in sectorStars)
                {
                    if (seenMaps.Add(s.Map))
                        stars.Add(s);
                }
            }
        }
        catch { }
        return stars;
    }

    private void OnMapRemoved(MapRemovedEvent ev)
    {
        var removed = ev.MapId;
        var q = AllEntityQuery<StarMapComponent>();
        var changed = false;
        while (q.MoveNext(out var uid, out var comp))
        {
            var count = comp.StarMap.RemoveAll(s => s.Map == removed);
            if (count > 0)
            {
                changed = true;
            }
        }
        if (changed) InvalidateCache();
    }

    private List<Star>? _cachedStars;
    private List<HyperlaneEdge>? _cachedEdges;

    private void EnsureCache()
    {
        if (_cachedStars != null && _cachedStars.Count > 0 && _cachedEdges != null) return;
        var stars = GetAllStars();
        if (stars.Count == 0)
        {
            try { _sectorStarMap?.UpdateAllStarMaps(); }
            catch { }
            stars = GetAllStars();
        }
        if (stars.Count == 0)
        { return; }
        stars.Sort((x, y) =>
        {
            var c = x.Map.GetHashCode().CompareTo(y.Map.GetHashCode());
            if (c != 0) return c;
            return string.Compare(x.Name, y.Name, StringComparison.Ordinal);
        });
        _cachedStars = stars;
        _cachedEdges = BuildHyperlanes(_cachedStars);
    }

    public List<Star> CollectStars()
    {
        EnsureCache();
        if (_cachedStars != null && _cachedStars.Count > 0) return new List<Star>(_cachedStars);
        return GetAllStars();
    }

    public List<Star> CollectStarsFresh(bool updateCache)
    {
        var stars = GetAllStars();
        if (updateCache && stars.Count > 0)
        {
            stars.Sort((x, y) =>
            {
                var c = x.Map.GetHashCode().CompareTo(y.Map.GetHashCode());
                if (c != 0) return c;
                return string.Compare(x.Name, y.Name, StringComparison.Ordinal);
            });
            _cachedStars = stars;
            _cachedEdges = BuildHyperlanes(_cachedStars);
        }
        return stars;
    }

    private List<HyperlaneEdge> BuildHyperlanes(List<Star> stars)
    {
        var edges = new List<HyperlaneEdge>();
        var edgeSet = new HashSet<(int a, int b)>();
        int n = stars.Count;
        if (n <= 1) return edges;

        var mapIndex = new Dictionary<MapId, int>(n);
        for (var i = 0; i < n; i++)
        {
            var map = stars[i].Map;
            if (!mapIndex.ContainsKey(map)) mapIndex[map] = i;
        }

        try
        {
            if (_prototypes.TryIndex<StarmapDataPrototype>("StarmapData", out var data))
            {
                var idToStarIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var def in data.Stars)
                {
                    for (var i = 0; i < n; i++)
                    {
                        if (Vector2.Distance(stars[i].Position, def.Position) < 0.1f)
                        {
                            idToStarIndex[def.Id] = i;
                            break;
                        }
                    }
                }

                foreach (var pair in data.Hyperlanes)
                {
                    if (pair.Length < 2) continue;
                    if (!idToStarIndex.TryGetValue(pair[0], out var idxA)) continue;
                    if (!idToStarIndex.TryGetValue(pair[1], out var idxB)) continue;
                    if (idxA == idxB) continue;

                    var a = Math.Min(idxA, idxB);
                    var b = Math.Max(idxA, idxB);
                    if (edgeSet.Add((a, b)))
                        edges.Add(new HyperlaneEdge(a, b));
                }
            }
        }
        catch { }

        if (_blockedHyperlanes.Count > 0)
        {
            for (var i = edges.Count - 1; i >= 0; i--)
            {
                var e = edges[i];
                var mapA = stars[e.A].Map;
                var mapB = stars[e.B].Map;
                var pair = NormalizeMapPair(mapA, mapB);
                if (_blockedHyperlanes.Contains(pair))
                {
                    edges.RemoveAt(i);
                    var a = Math.Min(e.A, e.B);
                    var b = Math.Max(e.A, e.B);
                    edgeSet.Remove((a, b));
                }
            }
        }

        if (_manualHyperlanes.Count > 0)
        {
            foreach (var manualPair in _manualHyperlanes)
            {
                if (!mapIndex.TryGetValue(manualPair.a, out var idxA)) continue;
                if (!mapIndex.TryGetValue(manualPair.b, out var idxB)) continue;
                if (idxA == idxB) continue;
                var a = Math.Min(idxA, idxB);
                var b = Math.Max(idxA, idxB);
                if (edgeSet.Add((a, b))) edges.Add(new HyperlaneEdge(a, b));
            }
        }

        return edges;
    }

    public List<HyperlaneEdge> GetHyperlanesCached()
    {
        EnsureCache();
        if (_cachedEdges != null) return _cachedEdges;
        var stars = GetAllStars();
        return BuildHyperlanes(stars);
    }

    public void InvalidateCache(bool refreshConsoles = true)
    {
        _cachedStars = null;
        _cachedEdges = null;
        if (refreshConsoles)
        { try { _shuttleConsole.RefreshStarMapForOpenConsoles(); } catch { } }
    }

    public void RefreshConsoles()
    { try { _shuttleConsole.RefreshStarMapForOpenConsoles(); } catch { } }

    private void OnDriveExamineEvent(EntityUid uid, BluespaceDriveComponent component, ExaminedEvent args)
    {
        var readyIn = TimeSpan.Zero;
        if (component.CooldownEndsAt > IoCManager.Resolve<IGameTiming>().CurTime) readyIn = component.CooldownEndsAt - IoCManager.Resolve<IGameTiming>().CurTime;
        args.PushMarkup($"Bluespace drive cooldown: {(readyIn > TimeSpan.Zero ? (int)readyIn.TotalSeconds + "s" : "ready")}");
    }
}
