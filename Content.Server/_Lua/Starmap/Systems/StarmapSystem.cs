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
        var starMapQuery = AllEntityQuery<StarMapComponent>();
        while (starMapQuery.MoveNext(out var uid, out var starMap))
        { foreach (var s in starMap.StarMap) { if (_mapManager.MapExists(s.Map)) stars.Add(s); } }
        try
        { if (_sectorStarMap != null) { var sectorStars = _sectorStarMap.GetSectorStars(); stars.AddRange(sectorStars); } }
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
        for (var i = 0; i < n; i++)
        {
            var dists = new List<(int j, float d)>(n - 1);
            for (var j = 0; j < n; j++)
            {
                if (i == j) continue;
                var d = Vector2.Distance(stars[i].Position, stars[j].Position);
                if (d <= _hyperlaneMaxDistance) dists.Add((j, d));
            }
            dists.Sort((a, b) => a.d.CompareTo(b.d));
            var take = Math.Min(_hyperlaneNeighbors, dists.Count);
            for (var k = 0; k < take; k++)
            {
                var j = dists[k].j;
                var a = Math.Min(i, j);
                var b = Math.Max(i, j);
                if (edgeSet.Add((a, b))) edges.Add(new HyperlaneEdge(a, b));
            }
        }
        var degree = new int[n];
        foreach (var e in edges)
        {
            degree[e.A]++;
            degree[e.B]++;
        }
        for (var i = 0; i < n; i++)
        {
            if (degree[i] > 0) continue;
            var bestJ = -1; var bestD = float.MaxValue;
            for (var j = 0; j < n; j++)
            {
                if (i == j) continue;
                var d = Vector2.Distance(stars[i].Position, stars[j].Position);
                if (d < bestD) { bestD = d; bestJ = j; }
            }
            if (bestJ != -1)
            {
                var a = Math.Min(i, bestJ);
                var b = Math.Max(i, bestJ);
                if (edgeSet.Add((a, b)))
                {
                    edges.Add(new HyperlaneEdge(a, b));
                    degree[i]++; degree[bestJ]++;
                }
            }
        }
        var parent = new int[n];
        for (var i = 0; i < n; i++) parent[i] = i;
        int Find(int x)
        {
            while (parent[x] != x) x = parent[x] = parent[parent[x]];
            return x;
        }
        void Union(int x, int y)
        {
            x = Find(x); y = Find(y);
            if (x != y) parent[y] = x;
        }
        foreach (var e in edges) Union(e.A, e.B);
        Func<int> CountComponents = () =>
        {
            var set = new HashSet<int>();
            for (var i = 0; i < n; i++) set.Add(Find(i));
            return set.Count;
        };
        while (CountComponents() > 1)
        {
            var best = (a: -1, b: -1, d: float.MaxValue);
            for (var i = 0; i < n; i++)
            {
                for (var j = i + 1; j < n; j++)
                {
                    if (Find(i) == Find(j)) continue;
                    var d = Vector2.Distance(stars[i].Position, stars[j].Position);
                    if (d < best.d) best = (i, j, d);
                }
            }
            if (best.a == -1 || best.b == -1) break;
            var a2 = Math.Min(best.a, best.b);
            var b2 = Math.Max(best.a, best.b);
            if (edgeSet.Add((a2, b2)))
            { edges.Add(new HyperlaneEdge(a2, b2)); Union(a2, b2); }
            else
            { Union(a2, b2); }
        }
        try
        {
            if (_sectors.TryGetMapId("AsteroidSectorDefault", out var asteroidMap))
            {
                var asteroidIdx = stars.FindIndex(s => s.Map == asteroidMap);
                if (asteroidIdx >= 0)
                {
                    var mainSectors = new[] { "TypanSector", "PirateSector", "MercenarySector" };
                    foreach (var sectorId in mainSectors)
                    {
                        if (_sectors.TryGetMapId(sectorId, out var sectorMap))
                        {
                            var sectorIdx = stars.FindIndex(s => s.Map == sectorMap);
                            if (sectorIdx >= 0 && sectorIdx != asteroidIdx)
                            {
                                var a = Math.Min(asteroidIdx, sectorIdx);
                                var b = Math.Max(asteroidIdx, sectorIdx);
                                if (edgeSet.Add((a, b))) edges.Add(new HyperlaneEdge(a, b));
                            }
                        }
                    }
                }
            }
        }
        catch { }
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
        { try { _shuttleConsole.RefreshShuttleConsoles(); } catch { } }
    }

    public void RefreshConsoles()
    { try { _shuttleConsole.RefreshShuttleConsoles(); } catch { } }

    private void OnDriveExamineEvent(EntityUid uid, BluespaceDriveComponent component, ExaminedEvent args)
    {
        var readyIn = TimeSpan.Zero;
        if (component.CooldownEndsAt > IoCManager.Resolve<IGameTiming>().CurTime) readyIn = component.CooldownEndsAt - IoCManager.Resolve<IGameTiming>().CurTime;
        args.PushMarkup($"Bluespace drive cooldown: {(readyIn > TimeSpan.Zero ? (int)readyIn.TotalSeconds + "s" : "ready")}");
    }
}
