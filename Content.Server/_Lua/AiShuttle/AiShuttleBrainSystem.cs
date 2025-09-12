// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.
using Content.Server._Mono.FireControl;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Power.EntitySystems;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._Lua.AiShuttle;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server._Lua.AiShuttle;

public sealed partial class AiShuttleBrainSystem : EntitySystem
{
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly FireControlSystem _fire = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly ShuttleConsoleSystem _console = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;

    private const float SensorsHz = 5f;
    private const float ControlHz = 9f;

    private float _sensorAccum;
    private float _controlAccum;
    private readonly HashSet<EntityUid> _presetApplied = new();
    private readonly Dictionary<MapId, (EntityUid target, float score)> _globalFocus = new();
    private readonly Dictionary<MapId, float> _lastWingCheck = new();
    private const float WING_CHECK_INTERVAL = 30f;

    [Dependency] private readonly IGameTiming _gameTiming = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AiShuttleKillSwitchComponent, EntityTerminatingEvent>(OnKillSwitchEntityTerminating);
    }

    private void ApplyPresets(EntityUid gridUid, ref AiShuttleBrainComponent brain)
    {
        if (_presetApplied.Contains(gridUid)) return;
        var applied = false;
        if (brain.Preset != null)
        {
            if (_prototypes.TryIndex(brain.Preset, out AiShuttlePresetPrototype? preset))
            {
                ApplyFromPrototype(ref brain, preset);
                applied = true;
            }
        }
        else if (TryComp<AiShuttlePresetComponent>(gridUid, out var presetComp))
        {
            ApplyFromComponent(ref brain, presetComp);
            applied = true;
        }
        if (applied) { _presetApplied.Add(gridUid); }
    }

    private static void ApplyFromComponent(ref AiShuttleBrainComponent brain, AiShuttlePresetComponent preset)
    {
        brain.AnchorEntityName = preset.AnchorEntityName;
        brain.FallbackAnchor = preset.FallbackAnchor;
        brain.MinPatrolRadius = preset.MinPatrolRadius;
        brain.MaxPatrolRadius = preset.MaxPatrolRadius;
        brain.FightRangeMin = preset.FightRangeMin;
        brain.FightRangeMax = preset.FightRangeMax;
        brain.FtlMinDistance = preset.FtlMinDistance;
        brain.FtlExitOffset = preset.FtlExitOffset;
        brain.PostFtlStabilizeSeconds = preset.PostFtlStabilizeSeconds;
        brain.FtlCooldownSeconds = preset.FtlCooldownSeconds;
        brain.RetreatHealthFraction = preset.RetreatHealthFraction;
        brain.AvoidStationFire = preset.AvoidStationFire;
        brain.ForbidRamming = preset.ForbidRamming;
        brain.TargetShuttlesOnly = preset.TargetShuttlesOnly;
        brain.TargetShuttles = preset.TargetShuttles;
        brain.TargetDebris = preset.TargetDebris;
        brain.MinSeparation = preset.MinSeparation;
        brain.MaxWeaponRange = preset.MaxWeaponRange;
        brain.ForwardAngleOffset = preset.ForwardAngleOffset;
        if (preset.PatrolSectorEnabled is { } pse) brain.PatrolSectorEnabled = pse;
        if (preset.PatrolSectorCenter is { } psc) brain.PatrolSectorCenter = psc;
        if (preset.PatrolSectorRadius is { } psr) brain.PatrolSectorRadius = psr;
        if (preset.PatrolHoldSeconds is { } phs) brain.PatrolHoldSeconds = phs;
    }

    private static void ApplyFromPrototype(ref AiShuttleBrainComponent brain, AiShuttlePresetPrototype preset)
    {
        brain.AnchorEntityName = preset.AnchorEntityName;
        brain.FallbackAnchor = preset.FallbackAnchor;
        brain.MinPatrolRadius = preset.MinPatrolRadius;
        brain.MaxPatrolRadius = preset.MaxPatrolRadius;
        brain.FightRangeMin = preset.FightRangeMin;
        brain.FightRangeMax = preset.FightRangeMax;
        brain.FtlMinDistance = preset.FtlMinDistance;
        brain.FtlExitOffset = preset.FtlExitOffset;
        brain.PostFtlStabilizeSeconds = preset.PostFtlStabilizeSeconds;
        brain.FtlCooldownSeconds = preset.FtlCooldownSeconds;
        brain.RetreatHealthFraction = preset.RetreatHealthFraction;
        brain.AvoidStationFire = preset.AvoidStationFire;
        brain.ForbidRamming = preset.ForbidRamming;
        brain.TargetShuttlesOnly = preset.TargetShuttlesOnly;
        brain.TargetShuttles = preset.TargetShuttles;
        brain.TargetDebris = preset.TargetDebris;
        brain.MinSeparation = preset.MinSeparation;
        brain.MaxWeaponRange = preset.MaxWeaponRange;
        brain.ForwardAngleOffset = preset.ForwardAngleOffset;
        if (preset.PatrolSectorEnabled is { } pse) brain.PatrolSectorEnabled = pse;
        if (preset.PatrolSectorCenter is { } psc) brain.PatrolSectorCenter = psc;
        if (preset.PatrolSectorRadius is { } psr) brain.PatrolSectorRadius = psr;
        if (preset.PatrolHoldSeconds is { } phs) brain.PatrolHoldSeconds = phs;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _sensorAccum += frameTime;
        _controlAccum += frameTime;
        var doSensors = _sensorAccum >= 1f / SensorsHz;
        var doControl = _controlAccum >= 1f / ControlHz;
        if (!doSensors && !doControl) return;
        if (doSensors)
        {
            _sensorAccum = 0f;
            _globalFocus.Clear();
            var processedMaps = new HashSet<MapId>();
            var aiQuery = EntityQueryEnumerator<AiShuttleBrainComponent, TransformComponent>();
            while (aiQuery.MoveNext(out var uid, out var brain, out var xform))
            {
                if (!brain.Enabled) continue;
                if (xform.MapID == MapId.Nullspace) continue;
                if (processedMaps.Contains(xform.MapID)) continue;
                var currentTime = (float)_gameTiming.CurTime.TotalSeconds;
                if (!_lastWingCheck.TryGetValue(xform.MapID, out var lastCheck) || currentTime - lastCheck >= WING_CHECK_INTERVAL)
                {
                    AssignAiShuttleWings(xform.MapID);
                    _lastWingCheck[xform.MapID] = currentTime;
                }
                processedMaps.Add(xform.MapID);
            }
        }
        if (doControl) _controlAccum = 0f;
        var q = EntityQueryEnumerator<AiShuttleBrainComponent, TransformComponent, PhysicsComponent, MapGridComponent>();
        while (q.MoveNext(out var uid, out var brain, out var xform, out var body, out _))
        {
            if (!brain.Enabled) continue;
            ApplyPresets(uid, ref brain);
            ProcessKillSwitch(uid, ref brain, xform, frameTime);
            var anchor = GetAnchorWorld(brain, xform.MapID);
            var sectorCenter = brain.PatrolSectorEnabled ? brain.PatrolSectorCenter : anchor;
            var pos = _xform.GetWorldPosition(xform);
            if (brain.ConsoleRetryTimer > 0f) brain.ConsoleRetryTimer = MathF.Max(0f, brain.ConsoleRetryTimer - frameTime);
            EnsurePilot(uid, ref brain);
            EntityUid? targetGrid = null;
            Vector2 targetPos = default;
            Vector2 aimPos = default;
            if (doSensors)
            {
                var bestScore = float.MinValue;
                var gridQuery = EntityQueryEnumerator<Content.Server.Shuttles.Components.ShuttleComponent, TransformComponent>();
                while (gridQuery.MoveNext(out var gridUid, out _, out var gxform))
                {
                    if (gridUid == uid) continue;
                    if (gxform.MapID != xform.MapID) continue;
                    if (!HasComp<ShuttleDeedComponent>(gridUid)) continue;
                    if (HasComp<AiShuttleBrainComponent>(gridUid)) continue;
                    var gp = _xform.GetWorldPosition(gxform);
                    if (brain.RespectFtlExclusions && IsPositionInAnyFtlExclusion(gp, gxform.MapID, brain.FtlExclusionBuffer)) continue;
                    if (!GridHasGunneryAndConsole(gridUid)) continue;
                    if (!TryGetEnemyGunneryServerPos(gridUid, out var gcsPos)) continue;
                    if (brain.PatrolSectorEnabled)
                    {
                        var distToCenter = (gp - sectorCenter).Length();
                        if (distToCenter > brain.PatrolSectorRadius) continue;
                    }
                    else
                    {
                        var distToAnchor = (gp - anchor).Length();
                        if (distToAnchor < brain.MinPatrolRadius || distToAnchor > brain.MaxPatrolRadius) continue;
                    }
                    var dist = (gp - pos).Length();
                    if (dist > brain.CombatTriggerRadius) continue;
                    var score = -dist;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        targetGrid = gridUid;
                        targetPos = gp;
                        aimPos = gcsPos;
                    }
                }
                if (targetGrid != null)
                {
                    if (!_globalFocus.TryGetValue(xform.MapID, out var cur) || bestScore > cur.score) _globalFocus[xform.MapID] = (targetGrid.Value, bestScore);
                }
                if (targetGrid != null)
                {
                    brain.CurrentTarget = targetGrid;
                    brain.LastKnownTargetPos = aimPos != default ? aimPos : targetPos;
                    brain.FacingMarker = brain.LastKnownTargetPos;
                }
                else
                {
                    brain.CurrentTarget = null;
                    brain.LastKnownTargetPos = null;
                    brain.FacingMarker = null;
                }
                HandleCombatModeTransition(uid, ref brain, targetGrid != null);
            }
            if (doControl)
            {
                if (brain.WaitingForConsole) continue;
                if (brain.StabilizeTimer > 0f)
                    brain.StabilizeTimer = MathF.Max(0f, brain.StabilizeTimer - (1f / ControlHz));
                if (brain.FtlCooldownTimer > 0f)
                    brain.FtlCooldownTimer = MathF.Max(0f, brain.FtlCooldownTimer - (1f / ControlHz));
                if (brain.FormingWing)
                {
                    if (brain.WingSlot > 0 && brain.WingLeader is { } leader && !Deleted(leader))
                    {
                        ProcessWingmanBehavior(uid, ref brain, xform, body, 1f / ControlHz, null, Vector2.Zero, Vector2.Zero);
                        if (TryComp<TransformComponent>(leader, out var lXform) && TryComp<AiShuttleBrainComponent>(leader, out var lBrain))
                        {
                            var leaderPos = _xform.GetWorldPosition(lXform);
                            var leaderRot = _xform.GetWorldRotation(lXform) + Angle.FromDegrees(lBrain.ForwardAngleOffset);
                            var right = (leaderRot).RotateVec(Vector2.UnitX);
                            var lateral = (brain.WingSlot == 1 ? -1f : 1f) * lBrain.FormationSpacing;
                            var formTarget = leaderPos + right * lateral;
                            var wingmanPos = _xform.GetWorldPosition(xform);
                            var distToForm = (formTarget - wingmanPos).Length();
                            var distToLeader = (leaderPos - wingmanPos).Length();
                            if (distToLeader >= 64f && distToLeader <= 72f) { }
                        }
                    }
                    else if (brain.WingSlot == 0)
                    { brain.FormingWing = false; }
                    else
                    {
                        DriveViaConsole(uid, brain, body, 1f / ControlHz, Vector2.Zero, Vector2.Zero, 0f, true, 0);
                    }
                    continue;
                }
                if (brain.WingSlot > 0)
                {
                    if (brain.WingLeader is { } missingLeader && Deleted(missingLeader))
                    {
                        brain.WingLeader = null;
                        brain.WingSlot = -1;
                        brain.FormingWing = false;
                    }
                    else if (brain.WingLeader is { } leader && !Deleted(leader) && TryComp<TransformComponent>(leader, out var lXform))
                    {
                        if (lXform.MapID == xform.MapID)
                        {
                            var leaderPos = _xform.GetWorldPosition(lXform);
                            var wingmanPos = _xform.GetWorldPosition(xform);
                            var distToLeader = (leaderPos - wingmanPos).Length();
                            if (distToLeader > 200f)
                            {
                                brain.WingLeader = null;
                                brain.WingSlot = -1;
                                brain.FormingWing = false;
                                if (_lastWingCheck.TryGetValue(xform.MapID, out _))
                                { _lastWingCheck[xform.MapID] = 0f; }
                            }
                        }
                        else
                        {
                            brain.WingLeader = null;
                            brain.WingSlot = -1;
                            brain.FormingWing = false;
                            if (_lastWingCheck.TryGetValue(xform.MapID, out _))
                            { _lastWingCheck[xform.MapID] = 0f; }
                        }
                    }
                    ProcessWingmanBehavior(uid, ref brain, xform, body, 1f / ControlHz, targetGrid, targetPos, aimPos); continue;
                }
                if (targetGrid == null && brain.CurrentTarget != null && brain.LastKnownTargetPos != null)
                {
                    targetGrid = brain.CurrentTarget;
                    targetPos = brain.LastKnownTargetPos.Value;
                    aimPos = targetPos;
                    brain.FacingMarker = targetPos;
                }
                if (targetGrid != null && brain.RespectFtlExclusions)
                {
                    if (TryComp<TransformComponent>(targetGrid.Value, out var tgx) && TryComp<AiShuttleBrainComponent>(uid, out var selfBrain))
                    {
                        var tpos = _xform.GetWorldPosition(tgx);
                        if (IsPositionInAnyFtlExclusion(tpos, tgx.MapID, selfBrain.FtlExclusionBuffer))
                        { targetGrid = null; }
                    }
                }
                if (targetGrid != null)
                { ProcessCombatBehavior(uid, ref brain, xform, body, pos, targetGrid.Value, targetPos, aimPos, 1f / ControlHz); }
                else
                { ProcessPatrolBehavior(uid, ref brain, xform, body, pos, sectorCenter, 1f / ControlHz); }
            }
        }
    }

    private bool TryGetEnemyGunneryServerPos(EntityUid enemyGrid, out Vector2 worldPos)
    {
        var q = EntityQueryEnumerator<FireControlServerComponent, TransformComponent>();
        while (q.MoveNext(out var serverUid, out _, out var xform))
        {
            if (xform.GridUid == enemyGrid)
            {
                worldPos = _xform.GetWorldPosition(xform);
                return true;
            }
        }
        worldPos = default;
        return false;
    }

    private bool GridHasGunneryAndConsole(EntityUid gridUid)
    {
        var hasConsole = false;
        var hasGunnery = false;
        var consoleQuery = EntityQueryEnumerator<ShuttleConsoleComponent, TransformComponent>();
        while (consoleQuery.MoveNext(out var cUid, out _, out var cxform))
        {
            if (cxform.GridUid == gridUid)
            {
                hasConsole = true;
                break;
            }
        }
        var gQuery = EntityQueryEnumerator<FireControlServerComponent, TransformComponent>();
        while (gQuery.MoveNext(out _, out _, out var gxform))
        {
            if (gxform.GridUid == gridUid)
            {
                hasGunnery = true;
                break;
            }
        }
        return hasConsole && hasGunnery;
    }

    private bool IsPositionInAnyFtlExclusion(Vector2 worldPos, MapId mapId, float buffer)
    {
        var q = EntityQueryEnumerator<FTLExclusionComponent, TransformComponent>();
        while (q.MoveNext(out var excl, out var xform))
        {
            if (!excl.Enabled) continue;
            if (xform.MapID != mapId) continue;
            var center = _xform.GetWorldPosition(xform);
            var range = excl.Range + buffer;
            if ((worldPos - center).Length() <= range) return true;
        }
        return false;
    }

    private bool TryGetNearestFtlExclusion(Vector2 worldPos, MapId mapId, float buffer, out Vector2 center, out float radius)
    {
        center = default;
        radius = 0f;
        var found = false;
        var minDist = float.MaxValue;
        var q = EntityQueryEnumerator<FTLExclusionComponent, TransformComponent>();
        while (q.MoveNext(out var excl, out var xform))
        {
            if (!excl.Enabled) continue;
            if (xform.MapID != mapId) continue;
            var c = _xform.GetWorldPosition(xform);
            var r = excl.Range + buffer;
            var d = (worldPos - c).Length();
            if (d < minDist)
            {
                minDist = d;
                center = c;
                radius = r;
                found = true;
            }
        }
        return found;
    }

    private void StopAiOnGrid(EntityUid grid)
    {
        if (!TryComp<AiShuttleBrainComponent>(grid, out var brain)) return;
        StopAiOnGrid(grid, ref brain);
    }

    private void StopAiOnGrid(EntityUid grid, ref AiShuttleBrainComponent brain)
    {
        if (brain.PilotEntity is { } pilot && !Deleted(pilot))
        {
            _console.RemovePilot(pilot);
            QueueDel(pilot);
            brain.PilotEntity = null;
        }
        RemComp<AiShuttleBrainComponent>(grid);
    }

    private void EnsurePilot(EntityUid grid, ref AiShuttleBrainComponent brain)
    {
        if (brain.PilotEntity is { } pilot && !Deleted(pilot) && TryComp<PilotComponent>(pilot, out var pilotComp) && pilotComp.Console != null)
        {
            if (brain.WaitingForConsole)
            {
                brain.WaitingForConsole = false;
                brain.ConsoleRetryTimer = 0f;
            }
            return;
        }
        if (brain.WaitingForConsole && brain.ConsoleRetryTimer > 0f) return;
        var consoleQ = EntityQueryEnumerator<ShuttleConsoleComponent, TransformComponent>();
        EntityUid? consoleUid = null;
        while (consoleQ.MoveNext(out var cUid, out _, out var cxform))
        {
            if (cxform.GridUid == grid)
            {
                consoleUid = cUid;
                break;
            }
        }
        if (consoleUid == null)
        {
            brain.WaitingForConsole = true;
            brain.ConsoleRetryTimer = MathF.Max(brain.ConsoleRetryTimer, brain.ConsoleRetryIntervalSeconds);
            return;
        }
        if (brain.PilotEntity == null || Deleted(brain.PilotEntity.Value))
        {
            var proto = _prototypes.HasIndex<EntityPrototype>("MobShuttleAIPilotNF") ? "MobShuttleAIPilotNF" : "MobShuttleAIPilot";
            var spawn = Transform(consoleUid.Value).Coordinates;
            brain.PilotEntity = EntityManager.SpawnEntity(proto, spawn);
        }
        var pilotEnt = brain.PilotEntity!.Value;
        EnsureComp<InputMoverComponent>(pilotEnt);
        EnsureComp<PilotComponent>(pilotEnt);
        var p = Comp<PilotComponent>(pilotEnt);
        if (p.Console != consoleUid)
        { _console.AddPilot(consoleUid.Value, pilotEnt, Comp<ShuttleConsoleComponent>(consoleUid.Value)); }
        brain.WaitingForConsole = false;
        brain.ConsoleRetryTimer = 0f;
    }

    private Vector2 GetAnchorWorld(AiShuttleBrainComponent brain, MapId map)
    {
        if (!string.IsNullOrWhiteSpace(brain.AnchorEntityName))
        {
            var query = EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var meta, out var xform))
            {
                if (xform.MapID != map) continue;
                if (meta.EntityName == brain.AnchorEntityName) return _xform.GetWorldPosition(xform);
            }
        }
        return brain.FallbackAnchor;
    }
    private void ResetWingFormation(EntityUid uid, ref AiShuttleBrainComponent brain)
    {
        brain.WingLeader = null;
        brain.WingSlot = -1;
        brain.FormingWing = false;
        if (brain.WingSlot == 0)
        {
            var wingQuery = EntityQueryEnumerator<AiShuttleBrainComponent>();
            while (wingQuery.MoveNext(out var wingUid, out var wingBrain))
            {
                if (wingBrain.WingLeader == uid)
                {
                    wingBrain.WingLeader = null;
                    wingBrain.WingSlot = -1;
                    wingBrain.FormingWing = false;
                }
            }
        }
        else if (brain.WingSlot > 0) { }
    }
}
