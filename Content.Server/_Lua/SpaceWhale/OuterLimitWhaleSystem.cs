// LuaWorld/LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld/LuaCorp
// See AGPLv3.txt for details.

using Content.Server._Goobstation.MobCaller;
using Content.Server.NPC;
using Content.Server.NPC.Systems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Popups;
using Content.Shared._Goobstation.SpaceWhale;
using Content.Shared.Lua.CLVar;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server._Lua.SpaceWhale;

public sealed class OuterLimitWhaleSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    private bool _enabled;
    private float _outerLimitRadius;
    private float _checkIntervalMinutes;
    private float _spawnChance;
    private float _playerClusterRadius;
    private float _safeZoneRadius;
    private float _despawnLifetimeMinutes;

    private TimeSpan _nextCheckTime;
    private TimeSpan _nextMaintenanceTime;
    private static readonly TimeSpan MaintenanceInterval = TimeSpan.FromSeconds(1);
    private const float WhaleTargetRange = 2000f;
    private const string WhalePrototype = "MobSpaceWhale";
    private const string WhaleLootPrototype = "SpaceWhaleLootBox";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpaceWhaleComponent, MapInitEvent>(OnWhaleMapInit);
        SubscribeLocalEvent<SpaceWhaleComponent, MobStateChangedEvent>(OnWhaleStateChanged);

        Subs.CVar(_cfg, CLVars.SpaceWhaleEnabled, v => _enabled = v, true);
        Subs.CVar(_cfg, CLVars.SpaceWhaleOuterLimitRadius, v => _outerLimitRadius = v, true);
        Subs.CVar(_cfg, CLVars.SpaceWhaleCheckIntervalMinutes, v => _checkIntervalMinutes = v, true);
        Subs.CVar(_cfg, CLVars.SpaceWhaleSpawnChance, v => _spawnChance = v, true);
        Subs.CVar(_cfg, CLVars.SpaceWhalePlayerClusterRadius, v => _playerClusterRadius = v, true);
        Subs.CVar(_cfg, CLVars.SpaceWhaleSafeZoneRadius, v => _safeZoneRadius = v, true);
        Subs.CVar(_cfg, CLVars.SpaceWhaleDespawnLifetimeMinutes, v => _despawnLifetimeMinutes = v, true);

        _nextCheckTime = _timing.CurTime + TimeSpan.FromMinutes(_checkIntervalMinutes);
        _nextMaintenanceTime = _timing.CurTime + MaintenanceInterval;
    }

    private void OnWhaleMapInit(EntityUid uid, SpaceWhaleComponent comp, MapInitEvent args)
    {
        comp.SpawnTime = _timing.CurTime;
    }

    private void OnWhaleStateChanged(EntityUid uid, SpaceWhaleComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;
        var xform = Transform(uid);
        var worldPos = _transform.GetWorldPosition(xform);
        Spawn(WhaleLootPrototype, new MapCoordinates(worldPos, xform.MapID));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_enabled)
            return;

        if (_timing.CurTime >= _nextMaintenanceTime)
        {
            _nextMaintenanceTime = _timing.CurTime + MaintenanceInterval;
            EnforceSafeZone();
            EnforceDespawnTimer();
            UpdateWhaleTargets();
        }

        if (_timing.CurTime < _nextCheckTime)
            return;

        _nextCheckTime = _timing.CurTime + TimeSpan.FromMinutes(_checkIntervalMinutes);
        PerformSpawnCheck();
    }
    private void EnforceSafeZone()
    {
        var safeZone2 = _safeZoneRadius * _safeZoneRadius;
        var query = EntityQueryEnumerator<SpaceWhaleComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out _, out var xform))
        {
            var worldPos = _transform.GetWorldPosition(xform);
            if (worldPos.LengthSquared() <= safeZone2)
            {
                QueueDel(uid);
            }
        }
    }
    private void EnforceDespawnTimer()
    {
        var maxLifetime = TimeSpan.FromMinutes(_despawnLifetimeMinutes);
        var query = EntityQueryEnumerator<SpaceWhaleComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime - comp.SpawnTime >= maxLifetime)
            {
                QueueDel(uid);
            }
        }
    }
    private void PerformSpawnCheck()
    {
        var outerLimit2 = _outerLimitRadius * _outerLimitRadius;
        var clusterRadius2 = _playerClusterRadius * _playerClusterRadius;
        var trackingQuery = EntityQueryEnumerator<SpaceWhaleTargetComponent, TransformComponent>();
        while (trackingQuery.MoveNext(out var uid, out var targetComp, out var xform))
        {
            var worldPos = _transform.GetWorldPosition(xform);
            if (worldPos.LengthSquared() <= outerLimit2)
            {
                if (Exists(targetComp.Entity) && !EntityManager.IsQueuedForDeletion(targetComp.Entity)) QueueDel(targetComp.Entity);
                RemComp<SpaceWhaleTargetComponent>(uid);
            }
        }
        var targetCleanup = EntityQueryEnumerator<SpaceWhaleTargetComponent, TransformComponent, MobStateComponent, MindContainerComponent>();
        while (targetCleanup.MoveNext(out var uid, out var targetComp, out var xform, out var mobState, out var mind))
        {
            var worldPos = _transform.GetWorldPosition(xform);
            var stillEligible =
                mind.HasMind &&
                mobState.CurrentState == MobState.Alive &&
                HasComp<ActorComponent>(uid) &&
                worldPos.LengthSquared() > outerLimit2;
            if (!stillEligible || !Exists(targetComp.Entity) || EntityManager.IsQueuedForDeletion(targetComp.Entity))
            { RemComp<SpaceWhaleTargetComponent>(uid); }
        }
        var playersOutside = new List<(EntityUid Uid, Vector2 WorldPos)>();
        var playerQuery = EntityQueryEnumerator<MindContainerComponent, MobStateComponent, TransformComponent>();

        while (playerQuery.MoveNext(out var uid, out var mind, out var mobState, out var xform))
        {
            if (!mind.HasMind)
                continue;

            if (mobState.CurrentState != MobState.Alive)
                continue;
            if (!HasComp<ActorComponent>(uid))
                continue;
            var worldPos = _transform.GetWorldPosition(xform);
            if (worldPos.LengthSquared() > outerLimit2)
            {
                playersOutside.Add((uid, worldPos));
            }
        }

        if (playersOutside.Count == 0)
            return;
        var groups = ClusterPlayers(playersOutside, clusterRadius2);
        foreach (var group in groups)
        {
            if (GroupHasWhale(group))
                continue;
            if (_random.Prob(_spawnChance))
            {
                SpawnWhaleForGroup(group);
            }
        }
    }
    private List<List<(EntityUid Uid, Vector2 WorldPos)>> ClusterPlayers(
        List<(EntityUid Uid, Vector2 WorldPos)> players,
        float radius2)
    {
        var parent = new int[players.Count];
        for (var i = 0; i < parent.Length; i++)
            parent[i] = i;

        int Find(int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }
            return x;
        }

        void Union(int a, int b)
        {
            var ra = Find(a);
            var rb = Find(b);
            if (ra != rb)
                parent[ra] = rb;
        }

        for (var i = 0; i < players.Count; i++)
        {
            for (var j = i + 1; j < players.Count; j++)
            {
                var delta = players[i].WorldPos - players[j].WorldPos;
                if (delta.LengthSquared() <= radius2)
                {
                    Union(i, j);
                }
            }
        }

        var groups = new Dictionary<int, List<(EntityUid Uid, Vector2 WorldPos)>>();
        for (var i = 0; i < players.Count; i++)
        {
            var root = Find(i);
            if (!groups.ContainsKey(root)) groups[root] = new List<(EntityUid, Vector2)>();
            groups[root].Add(players[i]);
        }

        return new List<List<(EntityUid Uid, Vector2 WorldPos)>>(groups.Values);
    }
    private void SpawnWhaleForGroup(List<(EntityUid Uid, Vector2 WorldPos)> group)
    {
        var centroid = Vector2.Zero;
        foreach (var (_, pos) in group)
        {
            centroid += pos;
        }
        centroid /= group.Count;
        var angle = _random.NextFloat() * MathF.Tau;
        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var dist = _random.NextFloat(700f, 1000f);
        var spawnPos = centroid + direction * dist;
        var targetUid = group[0].Uid;
        var firstXform = Transform(targetUid);
        var mapId = firstXform.MapID;
        var dummy = Spawn(null, Transform(targetUid).Coordinates);
        _transform.SetParent(dummy, targetUid);
        var caller = EnsureComp<MobCallerComponent>(dummy);
        caller.SpawnProto = WhalePrototype;
        caller.MaxAlive = 1;
        caller.MinDistance = 700f;
        caller.MaxDistance = 1000f;
        caller.OcclusionDistance = 0f;
        caller.GridOcclusionDistance = 0f;
        caller.NeedAnchored = false;
        caller.NeedPower = false;
        caller.SpawnSpacing = TimeSpan.FromSeconds(1);
        caller.SpawnAccumulator = caller.SpawnSpacing;
        var targetComp = EnsureComp<SpaceWhaleTargetComponent>(targetUid);
        targetComp.Entity = dummy;
        foreach (var (memberUid, _) in group)
        { _popup.PopupEntity(Loc.GetString("space-whale-approaching"), memberUid, memberUid, PopupType.LargeCaution); }
        _popup.PopupEntity(Loc.GetString("space-whale-spotted"), targetUid, targetUid, PopupType.LargeCaution);
        _audio.PlayEntity(new SoundPathSpecifier("/Audio/_Goobstation/Ambience/SpaceWhale/leviathan-appear.ogg"), targetUid, targetUid, AudioParams.Default.WithVolume(1f));
        if (TryComp<TransformComponent>(dummy, out var whaleXform))
        {
            var toGroup = centroid - spawnPos;
            if (toGroup.LengthSquared() > 0.01f)
            {
                var faceAngle = MathF.Atan2(toGroup.Y, toGroup.X);
                _transform.SetWorldRotation(whaleXform, faceAngle);
            }
        }
    }

    private bool GroupHasWhale(List<(EntityUid Uid, Vector2 WorldPos)> group)
    {
        foreach (var (uid, _) in group)
        {
            if (!TryComp<SpaceWhaleTargetComponent>(uid, out var target)) continue;
            if (Exists(target.Entity) && !EntityManager.IsQueuedForDeletion(target.Entity)) return true;
        }
        return false;
    }

    private void UpdateWhaleTargets()
    {
        var whaleQuery = EntityQueryEnumerator<SpaceWhaleComponent, TransformComponent>();
        var nearby = new HashSet<EntityUid>();

        while (whaleQuery.MoveNext(out var whaleUid, out _, out var whaleXform))
        {
            nearby.Clear();
            _lookup.GetEntitiesInRange(whaleUid, WhaleTargetRange, nearby, LookupFlags.Dynamic | LookupFlags.Approximate);
            EntityUid? bestAliveTarget = null;
            var bestAliveDist2 = float.MaxValue;
            EntityUid? bestPowerTarget = null;
            var bestPowerDist2 = float.MaxValue;
            var whalePos = _transform.GetWorldPosition(whaleXform);

            foreach (var ent in nearby)
            {
                if (ent == whaleUid) continue;
                if (TryComp<MobStateComponent>(ent, out var mob) && mob.CurrentState == MobState.Alive && TryComp<MindContainerComponent>(ent, out var mind) && mind.HasMind && HasComp<ActorComponent>(ent) && TryComp<TransformComponent>(ent, out var aliveXform))
                {
                    var d2 = (_transform.GetWorldPosition(aliveXform) - whalePos).LengthSquared();
                    if (d2 < bestAliveDist2)
                    {
                        bestAliveDist2 = d2;
                        bestAliveTarget = ent;
                    }
                    continue;
                }
                if (HasComp<ApcPowerReceiverComponent>(ent) && _power.IsPowered(ent) && TryComp<TransformComponent>(ent, out var powerXform))
                {
                    var d2 = (_transform.GetWorldPosition(powerXform) - whalePos).LengthSquared();
                    if (d2 < bestPowerDist2)
                    {
                        bestPowerDist2 = d2;
                        bestPowerTarget = ent;
                    }
                }
            }
            var target = bestAliveTarget ?? bestPowerTarget;
            if (target == null) continue;
            _npc.SetBlackboard(whaleUid, "Target", target.Value);
            _npc.SetBlackboard(whaleUid, NPCBlackboard.FollowTarget, new EntityCoordinates(target.Value, Vector2.Zero));
        }
    }
}

