// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Shared._Lua.AmbientSpaceEffects;
using Content.Shared._Lua.SpaceHazards;
using Content.Shared.Radiation.Components;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Lua.SpaceHazards;

public sealed class SpaceHazardActivitySystem : EntitySystem
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(2);
    private const float CellSize = 512f;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    private TimeSpan _nextCheck;
    private float _maxActivationRange = 1024f;
    private readonly Dictionary<(MapId Map, int X, int Y), List<EntityUid>> _cells = new();
    private readonly Dictionary<EntityUid, HazardIndex> _index = new();
    private readonly HashSet<EntityUid> _active = new();
    private readonly HashSet<EntityUid> _seenThisPass = new();
    private readonly List<EntityUid> _activeScratch = new();
    public IReadOnlyCollection<EntityUid> ActiveHazards => _active;

    private readonly record struct HazardIndex(MapId Map, Vector2 Pos, float Range, int CellX, int CellY);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpaceHazardActivityComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SpaceHazardActivityComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(EntityUid uid, SpaceHazardActivityComponent activity, MapInitEvent args)
    { Register(uid, activity); }

    private void OnShutdown(EntityUid uid, SpaceHazardActivityComponent activity, ComponentShutdown args)
    { Unregister(uid); }

    private void Register(EntityUid uid, SpaceHazardActivityComponent activity)
    {
        if (_index.ContainsKey(uid)) Unregister(uid);
        if (!TryComp(uid, out TransformComponent? xform) || xform.MapID == MapId.Nullspace) return;
        var pos = _transform.GetWorldPosition(xform);
        var range = MathF.Max(activity.ActivationRange, 1f);
        _maxActivationRange = MathF.Max(_maxActivationRange, range);
        var cellX = CellCoord(pos.X);
        var cellY = CellCoord(pos.Y);
        var key = (xform.MapID, cellX, cellY);
        if (!_cells.TryGetValue(key, out var list))
        {
            list = new List<EntityUid>(8);
            _cells[key] = list;
        }
        list.Add(uid);
        _index[uid] = new HazardIndex(xform.MapID, pos, range, cellX, cellY);
        if (HasComp<SectorCelestialBodyComponent>(uid))
        {
            activity.Active = true;
            activity.LastSeenPlayer = _timing.CurTime;
            _active.Add(uid);
            return;
        }
        if (activity.Active) _active.Add(uid);
    }

    private void Unregister(EntityUid uid)
    {
        if (!_index.Remove(uid, out var info))
        {
            _active.Remove(uid);
            return;
        }
        var key = (info.Map, info.CellX, info.CellY);
        if (_cells.TryGetValue(key, out var list))
        {
            list.Remove(uid);
            if (list.Count == 0) _cells.Remove(key);
        }
        _active.Remove(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        if (now < _nextCheck) return;
        _nextCheck = now + CheckInterval;
        _seenThisPass.Clear();
        var celestialQuery = EntityQueryEnumerator<SectorCelestialBodyComponent, SpaceHazardActivityComponent>();
        while (celestialQuery.MoveNext(out var uid, out _, out var activity))
        {
            if (!_index.ContainsKey(uid)) Register(uid, activity);
            if (activity.Active) continue;
            activity.Active = true;
            activity.LastSeenPlayer = now;
            _active.Add(uid);
        }
        var playersByMap = BuildPlayerPositions();
        var cellRadius = Math.Max(1, (int) MathF.Ceiling(_maxActivationRange / CellSize));
        foreach (var (mapId, players) in playersByMap)
        {
            foreach (var playerPos in players)
            {
                var cx = CellCoord(playerPos.X);
                var cy = CellCoord(playerPos.Y);
                for (var dx = -cellRadius; dx <= cellRadius; dx++)
                {
                    for (var dy = -cellRadius; dy <= cellRadius; dy++)
                    {
                        if (!_cells.TryGetValue((mapId, cx + dx, cy + dy), out var hazards)) continue;
                        foreach (var uid in hazards)
                        {
                            if (!_index.TryGetValue(uid, out var info)) continue;
                            if (!TryComp(uid, out SpaceHazardActivityComponent? activity)) continue;
                            var rangeSq = info.Range * info.Range;
                            if ((playerPos - info.Pos).LengthSquared() > rangeSq) continue;
                            _seenThisPass.Add(uid);
                            activity.LastSeenPlayer = now;
                            if (activity.Active) continue;
                            activity.Active = true;
                            _active.Add(uid);
                        }
                    }
                }
            }
        }
        _activeScratch.Clear();
        _activeScratch.AddRange(_active);
        foreach (var uid in _activeScratch)
        {
            if (_seenThisPass.Contains(uid)) continue;
            if (HasComp<SectorCelestialBodyComponent>(uid)) continue;
            if (!TryComp(uid, out SpaceHazardActivityComponent? activity) || !activity.Active)
            {
                _active.Remove(uid);
                continue;
            }
            if (activity.LastSeenPlayer is not { } last)
            {
                activity.LastSeenPlayer = now;
                continue;
            }
            if (now - last < activity.IdleTimeout) continue;
            activity.Active = false;
            _active.Remove(uid);
            if (HasComp<AmbientSpaceFieldComponent>(uid) && TryComp(uid, out RadiationSourceComponent? source))
            { source.Enabled = false; }
        }
    }

    private Dictionary<MapId, List<Vector2>> BuildPlayerPositions()
    {
        var result = new Dictionary<MapId, List<Vector2>>();
        var query = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapID == MapId.Nullspace) continue;
            var pos = xform.GridUid is { } grid ? _transform.GetWorldPosition(grid) : _transform.GetWorldPosition(xform);
            if (!result.TryGetValue(xform.MapID, out var list))
            {
                list = new List<Vector2>();
                result[xform.MapID] = list;
            }
            list.Add(pos);
        }
        return result;
    }

    private static int CellCoord(float value) => (int) MathF.Floor(value / CellSize);

    public static bool IsActive(EntityUid uid, SpaceHazardActivityComponent? activity, IEntityManager entMan)
    {
        if (activity == null && !entMan.TryGetComponent(uid, out activity)) return true;
        return activity.Active;
    }
}
