// LuaWorld/LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld/LuaCorp
// See AGPLv3.txt for details.

using Content.Server._Goobstation.MobCaller;
using Content.Server.NPC;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Server.Popups;
using Content.Server.Power.SMES;
using Content.Server.Shuttles.Components;
using Content.Shared._Goobstation.SpaceWhale;
using Content.Shared.Lua.CLVar;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
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
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    private bool _enabled;
    private float _outerLimitRadius;
    private float _checkIntervalMinutes;
    private float _spawnChance;
    private float _playerClusterRadius;
    private float _safeZoneRadius;
    private float _despawnLifetimeMinutes;
    private float _exposureChancePerMinute;
    private float _exposureMaxChance;
    private float _exposureMinutesPerExtraWhale;
    private int _exposureMaxWhales;
    private float _exposureMinCheckIntervalMinutes;
    private float _exposureCheckHalveMinutes;

    private TimeSpan _nextCheckTime;
    private TimeSpan _nextMaintenanceTime;
    private TimeSpan _nextSteerTime;
    private static readonly TimeSpan MaintenanceInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SteerInterval = TimeSpan.FromSeconds(0.2);
    private const float WhaleTargetRange = 2000f;
    private const float VelocityEpsilonSq = 0.01f;
    private const string WhalePrototype = "MobSpaceWhale";
    private const string WhaleLootPrototype = "SpaceWhaleLootBox";

    public override void Initialize()
    {
        base.Initialize();

        UpdatesAfter.Add(typeof(NPCSteeringSystem));

        SubscribeLocalEvent<SpaceWhaleComponent, MapInitEvent>(OnWhaleMapInit);
        SubscribeLocalEvent<SpaceWhaleComponent, MobStateChangedEvent>(OnWhaleStateChanged);

        Subs.CVar(_cfg, CLVars.SpaceWhaleEnabled, v => _enabled = v, true);
        Subs.CVar(_cfg, CLVars.SpaceWhaleOuterLimitRadius, v => _outerLimitRadius = v, true);
        Subs.CVar(_cfg, CLVars.SpaceWhaleCheckIntervalMinutes, v => _checkIntervalMinutes = v, true);
        Subs.CVar(_cfg, CLVars.SpaceWhaleSpawnChance, v => _spawnChance = v, true);
        Subs.CVar(_cfg, CLVars.SpaceWhalePlayerClusterRadius, v => _playerClusterRadius = v, true);
        Subs.CVar(_cfg, CLVars.SpaceWhaleSafeZoneRadius, v => _safeZoneRadius = v, true);
        Subs.CVar(_cfg, CLVars.SpaceWhaleDespawnLifetimeMinutes, v => _despawnLifetimeMinutes = v, true);
        Subs.CVar(_cfg, CLVars.SpaceWhaleExposureChancePerMinute, v => _exposureChancePerMinute = v, true);
        Subs.CVar(_cfg, CLVars.SpaceWhaleExposureMaxChance, v => _exposureMaxChance = v, true);
        Subs.CVar(_cfg, CLVars.SpaceWhaleExposureMinutesPerExtraWhale, v => _exposureMinutesPerExtraWhale = v, true);
        Subs.CVar(_cfg, CLVars.SpaceWhaleExposureMaxWhales, v => _exposureMaxWhales = v, true);
        Subs.CVar(_cfg, CLVars.SpaceWhaleExposureMinCheckIntervalMinutes, v => _exposureMinCheckIntervalMinutes = v, true);
        Subs.CVar(_cfg, CLVars.SpaceWhaleExposureCheckHalveMinutes, v => _exposureCheckHalveMinutes = v, true);

        _nextCheckTime = _timing.CurTime + TimeSpan.FromMinutes(_checkIntervalMinutes);
        _nextMaintenanceTime = _timing.CurTime + MaintenanceInterval;
        _nextSteerTime = _timing.CurTime + SteerInterval;
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

        if (_timing.CurTime >= _nextSteerTime)
        {
            _nextSteerTime = _timing.CurTime + SteerInterval;
            SteerWhales();
        }

        if (_timing.CurTime >= _nextMaintenanceTime)
        {
            _nextMaintenanceTime = _timing.CurTime + MaintenanceInterval;
            EnforceSafeZone();
            EnforceDespawnTimer();
            UpdateWhaleTargets();
            UpdatePlayerExposure();
        }

        if (_timing.CurTime < _nextCheckTime)
            return;

        var maxExposure = PerformSpawnCheck();
        ScheduleNextSpawnCheck(maxExposure);
    }

    private const float WhaleStopRange = 1.5f;

    private void SteerWhales()
    {
        var query = EntityQueryEnumerator<SpaceWhaleComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var whale, out var xform))
        {
            Vector2 direction;
            var speedFactor = 1f;
            var hasTarget = false;

            if (whale.Target is { } target
                && Exists(target)
                && !EntityManager.IsQueuedForDeletion(target)
                && TryComp<TransformComponent>(target, out var targetXform)
                && xform.MapID == targetXform.MapID)
            {
                hasTarget = true;
                ExitIdle(whale);
                var whalePos = _transform.GetWorldPosition(xform);
                var targetPos = _transform.GetWorldPosition(targetXform);
                direction = targetPos - whalePos;
            }
            else
            {
                EnsureIdle(whale, xform);
                direction = whale.IdleDirection;
                speedFactor = whale.IdleSpeedFactor;
            }

            var distance = direction.Length();
            Vector2 desiredVelocity = Vector2.Zero;
            Vector2 moveDir = Vector2.Zero;

            if (distance > (hasTarget ? WhaleStopRange : 0.01f))
            {
                moveDir = direction / distance;
                float speed = 80f;
                if (TryComp<MovementSpeedModifierComponent>(uid, out var speedMod))
                    speed = MathF.Max(speedMod.CurrentRunningSpeed, speedMod.CurrentSprintSpeed);

                desiredVelocity = moveDir * (speed * speedFactor);
            }

            if (TryComp<PhysicsComponent>(uid, out var body))
            {
                var delta = desiredVelocity - body.LinearVelocity;
                if (delta.LengthSquared() > VelocityEpsilonSq)
                    _physics.SetLinearVelocity(uid, desiredVelocity, body: body);
            }

            if (TryComp<InputMoverComponent>(uid, out var mover))
            {
                mover.CurTickSprintMovement = moveDir;
                mover.LastInputTick = _timing.CurTick;
                mover.LastInputSubTick = ushort.MaxValue;
            }

            if (TryComp<NPCSteeringComponent>(uid, out var steering) &&
                steering.Status == SteeringStatus.NoPath)
            {
                steering.Status = SteeringStatus.Moving;
                steering.FailedPathCount = 0;
            }

            if (moveDir != Vector2.Zero)
                _transform.SetWorldRotation(xform, MathF.Atan2(moveDir.Y, moveDir.X));
        }
    }

    private void EnsureIdle(SpaceWhaleComponent whale, TransformComponent xform)
    {
        whale.Target = null;

        if (!whale.Idle || _timing.CurTime >= whale.IdleRedirectAt || whale.IdleDirection.LengthSquared() < 0.01f)
            PickIdleDirection(whale, xform);

        whale.Idle = true;
    }

    private void ExitIdle(SpaceWhaleComponent whale)
    {
        whale.Idle = false;
    }

    private void PickIdleDirection(SpaceWhaleComponent whale, TransformComponent xform)
    {
        var whalePos = _transform.GetWorldPosition(xform);
        var outward = whalePos.LengthSquared() > 1f ? whalePos.Normalized() : Vector2.UnitX;
        var randomAngle = _random.NextFloat() * MathF.Tau;
        var randomDir = new Vector2(MathF.Cos(randomAngle), MathF.Sin(randomAngle));
        var mixed = (randomDir + outward * 0.35f);
        if (mixed.LengthSquared() < 0.01f)
            mixed = randomDir;

        whale.IdleDirection = mixed.Normalized();
        whale.IdleRedirectAt = _timing.CurTime + TimeSpan.FromSeconds(MathF.Max(3f, whale.IdleRedirectSeconds));
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
    private void UpdatePlayerExposure()
    {
        var outerLimit2 = _outerLimitRadius * _outerLimitRadius;
        var playerQuery = EntityQueryEnumerator<MindContainerComponent, MobStateComponent, TransformComponent>();

        while (playerQuery.MoveNext(out var uid, out var mind, out var mobState, out var xform))
        {
            if (!mind.HasMind || mobState.CurrentState != MobState.Alive || !HasComp<ActorComponent>(uid))
            {
                RemComp<SpaceWhaleExposureComponent>(uid);
                continue;
            }

            var outside = _transform.GetWorldPosition(xform).LengthSquared() > outerLimit2;
            if (!outside)
            {
                RemComp<SpaceWhaleExposureComponent>(uid);
                continue;
            }

            if (!TryComp<SpaceWhaleExposureComponent>(uid, out var exposure))
            {
                exposure = EnsureComp<SpaceWhaleExposureComponent>(uid);
                exposure.EnteredAt = _timing.CurTime;
            }
        }
    }

    private float GetExposureMinutes(EntityUid uid)
    {
        if (!TryComp<SpaceWhaleExposureComponent>(uid, out var exposure))
            return 0f;

        return (float)(_timing.CurTime - exposure.EnteredAt).TotalMinutes;
    }

    private float GetGroupExposureMinutes(List<(EntityUid Uid, Vector2 WorldPos)> group)
    {
        var max = 0f;
        foreach (var (uid, _) in group)
            max = MathF.Max(max, GetExposureMinutes(uid));
        return max;
    }

    private float GetSpawnChance(float exposureMinutes)
    {
        return Math.Clamp(_spawnChance + exposureMinutes * _exposureChancePerMinute, 0f, _exposureMaxChance);
    }

    private int GetMaxWhales(float exposureMinutes)
    {
        if (_exposureMinutesPerExtraWhale <= 0f)
            return Math.Max(1, _exposureMaxWhales);

        var extra = (int)(exposureMinutes / _exposureMinutesPerExtraWhale);
        return Math.Clamp(1 + extra, 1, Math.Max(1, _exposureMaxWhales));
    }

    private void ScheduleNextSpawnCheck(float maxExposureMinutes)
    {
        var interval = _checkIntervalMinutes;
        if (_exposureCheckHalveMinutes > 0f && maxExposureMinutes > 0f)
        {
            var halvings = maxExposureMinutes / _exposureCheckHalveMinutes;
            interval /= MathF.Pow(2f, halvings);
        }

        interval = MathF.Max(_exposureMinCheckIntervalMinutes, interval);
        _nextCheckTime = _timing.CurTime + TimeSpan.FromMinutes(interval);
    }
    private float PerformSpawnCheck()
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
        var maxExposure = 0f;

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
                maxExposure = MathF.Max(maxExposure, GetExposureMinutes(uid));
            }
        }

        if (playersOutside.Count == 0)
            return 0f;

        var groups = ClusterPlayers(playersOutside, clusterRadius2);
        foreach (var group in groups)
        {
            var exposure = GetGroupExposureMinutes(group);
            maxExposure = MathF.Max(maxExposure, exposure);
            var chance = GetSpawnChance(exposure);
            var maxWhales = GetMaxWhales(exposure);

            if (TryGetGroupCaller(group, out var callerUid, out var caller))
            {
                // Only raise the ceiling; do not force rapid respawns every check.
                if (caller.MaxAlive < maxWhales)
                    caller.MaxAlive = maxWhales;

                continue;
            }

            if (_random.Prob(chance))
                SpawnWhaleForGroup(group, maxWhales);
        }

        return maxExposure;
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
    private void SpawnWhaleForGroup(List<(EntityUid Uid, Vector2 WorldPos)> group, int maxWhales)
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
        var dummy = Spawn(null, Transform(targetUid).Coordinates);
        _transform.SetParent(dummy, targetUid);
        var caller = EnsureComp<MobCallerComponent>(dummy);
        caller.SpawnProto = WhalePrototype;
        caller.MaxAlive = Math.Max(1, maxWhales);
        caller.MinDistance = 700f;
        caller.MaxDistance = 1000f;
        caller.OcclusionDistance = 0f;
        caller.GridOcclusionDistance = 0f;
        caller.NeedAnchored = false;
        caller.NeedPower = false;
        // Second+ whales from the same caller should be rare, not every second.
        caller.SpawnSpacing = TimeSpan.FromMinutes(12);
        caller.SpawnAccumulator = TimeSpan.Zero;
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

    private bool TryGetGroupCaller(
        List<(EntityUid Uid, Vector2 WorldPos)> group,
        out EntityUid callerUid,
        out MobCallerComponent caller)
    {
        foreach (var (uid, _) in group)
        {
            if (!TryComp<SpaceWhaleTargetComponent>(uid, out var target))
                continue;
            if (!Exists(target.Entity) || EntityManager.IsQueuedForDeletion(target.Entity))
                continue;
            if (!TryComp<MobCallerComponent>(target.Entity, out var found))
                continue;

            callerUid = target.Entity;
            caller = found;
            return true;
        }

        callerUid = default;
        caller = default!;
        return false;
    }

    private void UpdateWhaleTargets()
    {
        var whaleQuery = EntityQueryEnumerator<SpaceWhaleComponent, TransformComponent>();
        var range2 = WhaleTargetRange * WhaleTargetRange;
        var stickyRange2 = (WhaleTargetRange * 1.5f) * (WhaleTargetRange * 1.5f);

        while (whaleQuery.MoveNext(out var whaleUid, out var whale, out var whaleXform))
        {
            var whalePos = _transform.GetWorldPosition(whaleXform);
            var whaleMap = whaleXform.MapID;

            var target = FindNearestWithComponent<ThrusterComponent>(whalePos, whaleMap, range2)
                ?? FindNearestWithComponent<SmesComponent>(whalePos, whaleMap, range2)
                ?? FindNearestVisiblePlayer(whalePos, whaleMap, range2);

            if (target == null &&
                whale.Target is { } sticky &&
                IsVisiblePlayerTarget(sticky, whaleMap) &&
                TryComp<TransformComponent>(sticky, out var stickyXform))
            {
                var stickyD2 = (_transform.GetWorldPosition(stickyXform) - whalePos).LengthSquared();
                if (stickyD2 <= stickyRange2)
                    target = sticky;
            }

            if (target == null)
            {
                if (whale.Target != null || !whale.Idle)
                    EnsureIdle(whale, whaleXform);
                else if (_timing.CurTime >= whale.IdleRedirectAt)
                    PickIdleDirection(whale, whaleXform);

                whale.Target = null;
                continue;
            }

            ExitIdle(whale);
            whale.Target = target;
            _npc.SetBlackboard(whaleUid, "Target", target.Value);
            _npc.SetBlackboard(whaleUid, NPCBlackboard.FollowTarget, new EntityCoordinates(target.Value, Vector2.Zero));
        }
    }

    private EntityUid? FindNearestWithComponent<TComp>(Vector2 whalePos, MapId whaleMap, float range2)
        where TComp : IComponent
    {
        EntityUid? best = null;
        var bestDist2 = float.MaxValue;
        var query = EntityQueryEnumerator<TComp, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapID != whaleMap)
                continue;

            var d2 = (_transform.GetWorldPosition(xform) - whalePos).LengthSquared();
            if (d2 > range2 || d2 >= bestDist2)
                continue;

            bestDist2 = d2;
            best = uid;
        }

        return best;
    }

    private EntityUid? FindNearestVisiblePlayer(Vector2 whalePos, MapId whaleMap, float range2)
    {
        EntityUid? best = null;
        var bestDist2 = float.MaxValue;
        var playerQuery = EntityQueryEnumerator<ActorComponent, MindContainerComponent, MobStateComponent, TransformComponent>();
        while (playerQuery.MoveNext(out var playerUid, out _, out var mind, out var mob, out var playerXform))
        {
            if (!IsVisiblePlayer(playerUid, mind, mob, playerXform, whaleMap))
                continue;

            var d2 = (_transform.GetWorldPosition(playerXform) - whalePos).LengthSquared();
            if (d2 > range2 || d2 >= bestDist2)
                continue;

            bestDist2 = d2;
            best = playerUid;
        }

        return best;
    }

    private bool IsVisiblePlayerTarget(EntityUid uid, MapId whaleMap)
    {
        if (!Exists(uid) || EntityManager.IsQueuedForDeletion(uid))
            return false;
        if (!TryComp<ActorComponent>(uid, out _))
            return false;
        if (!TryComp<MindContainerComponent>(uid, out var mind) || !TryComp<MobStateComponent>(uid, out var mob))
            return false;
        if (!TryComp<TransformComponent>(uid, out var xform))
            return false;
        return IsVisiblePlayer(uid, mind, mob, xform, whaleMap);
    }

    private bool IsVisiblePlayer(
        EntityUid uid,
        MindContainerComponent mind,
        MobStateComponent mob,
        TransformComponent xform,
        MapId whaleMap)
    {
        if (!mind.HasMind || mob.CurrentState != MobState.Alive)
            return false;
        if (xform.MapID != whaleMap)
            return false;
        if (HasComp<InsideEntityStorageComponent>(uid))
            return false;
        return true;
    }
}

