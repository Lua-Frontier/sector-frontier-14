using System.Numerics;
using System.Linq;
using System.Collections.Generic;
using Content.Server._Mono.FireControl;
using Content.Server.Shuttles.Systems;
using Content.Server.Shuttles.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared._Mono.AiShuttle;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Content.Shared.Physics;
using Content.Shared.Damage;
using Content.Shared._NF.Shipyard.Components;

namespace Content.Server._Mono.AiShuttle;

/// <summary>
/// Lua: server AI brain for autonomous piloting and gunnery of shuttle grids - patrol, target selection, maneuver, fire
/// </summary>
public sealed class AiShuttleBrainSystem : EntitySystem
{
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly FireControlSystem _fire = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly RayCastSystem _raycast = default!;
    [Dependency] private readonly ShuttleConsoleSystem _console = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private const float SensorsHz = 5f;        // targets update (higher for aggressive AI)
    private const float ControlHz = 9f;        // piloting, weapons (higher for responsive control)

    private float _sensorAccum;
    private float _controlAccum;
    private readonly HashSet<EntityUid> _presetApplied = new();
    private readonly Dictionary<MapId, (EntityUid target, float score)> _globalFocus = new();
    // private readonly HashSet<MapId> _wingsProcessed = new(); // Wings/Squadrons disabled

    private void ApplyPresets(EntityUid gridUid, ref AiShuttleBrainComponent brain)
    {
        if (_presetApplied.Contains(gridUid))
            return;

        var applied = false;

        // 1) Component-based preset on the same grid
        if (TryComp<AiShuttlePresetComponent>(gridUid, out var presetComp))
        {
            ApplyFromComponent(ref brain, presetComp);
            applied = true;
        }
        else
        {
            // 2) Prototype-based preset matched by grid name
            if (TryComp<MetaDataComponent>(gridUid, out var meta))
            {
                var gridName = meta.EntityName;
                foreach (var presetProto in _prototypes.EnumeratePrototypes<AiShuttlePresetPrototype>())
                {
                    if (string.IsNullOrWhiteSpace(presetProto.NameMatch))
                        continue;
                    if (!string.Equals(presetProto.NameMatch, gridName, StringComparison.Ordinal))
                        continue;

                    ApplyFromPrototype(ref brain, presetProto);
                    applied = true;
                    break;
                }
            }
        }

        if (applied)
        {
            _presetApplied.Add(gridUid);
            Logger.DebugS("ai-shuttle", $"[{ToPrettyString(gridUid)}] Preset applied to brain");
        }
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
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _sensorAccum += frameTime;
        _controlAccum += frameTime;

        var doSensors = _sensorAccum >= 1f / SensorsHz;
        var doControl = _controlAccum >= 1f / ControlHz;

        if (!doSensors && !doControl)
            return;

        if (doSensors)
        {
            _sensorAccum = 0f;
            _globalFocus.Clear();
            // _wingsProcessed.Clear(); // Wings/Squadrons disabled
        }
        if (doControl) _controlAccum = 0f;

        var q = EntityQueryEnumerator<AiShuttleBrainComponent, TransformComponent, PhysicsComponent, MapGridComponent>();
        while (q.MoveNext(out var uid, out var brain, out var xform, out var body, out _))
        {
            if (!brain.Enabled)
                continue;

            // Apply preset once per grid (component or prototype by name)
            ApplyPresets(uid, ref brain);

            // Per-map: assign wings for "Perun" shuttles in groups of three (leader + 2 wingmen)
            // if (doSensors && !_wingsProcessed.Contains(xform.MapID))
            // {
            //     AssignPerunWings(xform.MapID);
            //     _wingsProcessed.Add(xform.MapID);
            // }

            var anchor = GetAnchorWorld(brain, xform.MapID);
            var pos = _xform.GetWorldPosition(xform);

            // Ensure we have an AI pilot entity subscribed to a console on this grid
            EnsurePilot(uid, ref brain);

            // Pick target shuttle grid: only ShuttleDeed with a pilotable console, within combat trigger radius
            EntityUid? targetGrid = null;
            Vector2 targetPos = default;
            Vector2 aimPos = default;

            if (doSensors)
            {
                var bestScore = float.MinValue;
                var gridQuery = EntityQueryEnumerator<Content.Server.Shuttles.Components.ShuttleComponent, TransformComponent>();
                while (gridQuery.MoveNext(out var gridUid, out _, out var gxform))
                {
                    if (gridUid == uid)
                        continue;
                    if (gxform.MapID != xform.MapID)
                        continue;
                    // Attack only grids that have ShuttleDeedComponent
                    if (!HasComp<ShuttleDeedComponent>(gridUid))
                        continue;
                    // Always ignore friendly AI-controlled shuttles
                    if (HasComp<AiShuttleBrainComponent>(gridUid))
                        continue;

                    // Must have a pilotable console to be a valid target
                    if (!TryGetEnemyShuttleConsolePos(gridUid, out var consolePos))
                        continue;

                    var gp = _xform.GetWorldPosition(gxform);
                    var distToAnchor = (gp - anchor).Length();
                    if (distToAnchor < brain.MinPatrolRadius || distToAnchor > brain.MaxPatrolRadius)
                        continue;

                    var dist = (gp - pos).Length();
                    // Only consider enemies within trigger radius
                    if (dist > brain.CombatTriggerRadius)
                        continue;

                    // Prefer closer target (simple focus)
                    var score = -dist;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        targetGrid = gridUid;
                        targetPos = gp;
                        aimPos = consolePos;
                    }
                }

                // Update global focus target for this map
                if (targetGrid != null)
                {
                    if (!_globalFocus.TryGetValue(xform.MapID, out var cur) || bestScore > cur.score)
                        _globalFocus[xform.MapID] = (targetGrid.Value, bestScore);
                }

                // Set or clear target state
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
            }

            // Control: if we have a target, engage; else patrol drift toward ring
            if (doControl)
            {
                // Decay stabilization timer
                if (brain.StabilizeTimer > 0f)
                    brain.StabilizeTimer = MathF.Max(0f, brain.StabilizeTimer - (1f / ControlHz));
                if (brain.FtlCooldownTimer > 0f)
                    brain.FtlCooldownTimer = MathF.Max(0f, brain.FtlCooldownTimer - (1f / ControlHz));

                // If this grid is a Perun wingman, hold formation relative to the leader and inherit leader's target
                // if (brain.WingSlot > 0 && brain.WingLeader is { } leader && !Deleted(leader) &&
                //     TryComp<TransformComponent>(leader, out var lXform) &&
                //     TryComp<AiShuttleBrainComponent>(leader, out var lBrain))
                // {
                //     var leaderPos = _xform.GetWorldPosition(lXform);
                //     var leaderRot = _xform.GetWorldRotation(lXform) + Angle.FromDegrees(lBrain.ForwardAngleOffset);
                //     var right = (leaderRot).RotateVec(Vector2.UnitX);
                //     var lateral = (brain.WingSlot == 1 ? -1f : 1f) * lBrain.FormationSpacing;
                //     var formTarget = leaderPos + right * lateral;
                //
                //     // Follow the leader's current target and facing if present
                //     if (lBrain.CurrentTarget != null)
                //     {
                //         targetGrid = lBrain.CurrentTarget;
                //         targetPos = lBrain.LastKnownTargetPos ?? _xform.GetWorldPosition(Transform(lBrain.CurrentTarget.Value));
                //         aimPos = lBrain.FacingMarker ?? lBrain.LastKnownTargetPos ?? targetPos;
                //         brain.FacingMarker = aimPos;
                //     }
                //
                //     var toForm = formTarget - pos;
                //     var distForm = toForm.Length();
                //
                //     // FTL rejoin: if the wingman fell far behind, micro-jump to the designated slot
                //     var rejoinDist = MathF.Max(lBrain.FtlMinDistance, lBrain.FormationSpacing * 4f);
                //     if (distForm > rejoinDist && brain.FtlCooldownTimer <= 0f && _shuttle.CanFTL(uid, out _))
                //     {
                //         var coords = new MapCoordinates(formTarget, xform.MapID);
                //         brain.StabilizeTimer = MathF.Max(brain.StabilizeTimer, brain.PostFtlStabilizeSeconds);
                //         brain.FtlCooldownTimer = brain.FtlCooldownSeconds;
                //         _shuttle.FTLToCoordinates(uid, Comp<Content.Server.Shuttles.Components.ShuttleComponent>(uid), _xform.ToCoordinates(coords), Angle.Zero);
                //         continue;
                //     }
                //
                //     var dirTo = distForm > 0f ? toForm / MathF.Max(distForm, 0.001f) : Vector2.Zero;
                //     var tangential = new Vector2(-dirTo.Y, dirTo.X) * 0.2f;
                //     var radialError = Math.Clamp(distForm / MathF.Max(1f, lBrain.FormationSpacing) - 1f, -1f, 1f);
                //     var requestBrake = distForm < 6f;
                //     DriveViaConsole(uid, brain, body, 1f / ControlHz, dirTo, tangential, radialError, requestBrake, 0);
                //
                //     // Coordinated fire: if a target exists and is in range, fire at the leader's aim point
                //     if (targetGrid != null)
                //     {
                //         var fireAt = aimPos == default ? targetPos : aimPos;
                //         if ((fireAt - pos).Length() <= brain.MaxWeaponRange + 1f)
                //             _fire.TryAimAndFireGrid(uid, targetGrid.Value, fireAt);
                //     }
                //     continue;
                // }
                // Use last known if sensor didn't tick
                if (targetGrid == null && brain.CurrentTarget != null && brain.LastKnownTargetPos != null)
                {
                    targetGrid = brain.CurrentTarget;
                    targetPos = brain.LastKnownTargetPos.Value;
                    aimPos = targetPos;
                    brain.FacingMarker = targetPos;
                }

                if (targetGrid != null)
                {
                    // Abort combat if target console destroyed (no longer valid)
                    if (!TryGetEnemyShuttleConsolePos(targetGrid.Value, out var stillAim))
                    {
                        brain.CurrentTarget = null;
                        brain.LastKnownTargetPos = null;
                        brain.FacingMarker = null;
                        continue;
                    }
                    aimPos = stillAim;

                    // FTL if far (use hull-aware distance when available)
                    var toTarget = targetPos - pos;
                    var distCenter = toTarget.Length();
                    var hullDist = ComputeHullDistance(uid, pos, targetPos, xform.MapID, targetGrid.Value);
                    var dist = hullDist > 0f ? hullDist : distCenter;
                    // Enforce hard separation: if closer than MinSeparation, push away
                    if (dist < brain.MinSeparation && dist > 0f)
                    {
                        var away = -toTarget / dist;
                        // Strong backoff and rotate to still face target
                        DriveViaConsole(uid, brain, body, 1f / ControlHz, toTarget / MathF.Max(dist, 0.001f), Vector2.Zero, -MathF.Max(0f, brain.MinSeparation - dist), true, 0);
                        // Also press brake by cancelling forward if we were moving in
                        // (implicit via negative radialError)
                        // Skip firing to focus on backing out
                        continue;
                    }
                    // Perform FTL only if far enough and cooldown elapsed
                    var ftlThreshold = MathF.Max(brain.FtlMinDistance, brain.MaxWeaponRange * 3f);
                    if (dist > ftlThreshold && brain.FtlCooldownTimer <= 0f && _shuttle.CanFTL(uid, out _))
                    {
                        // Micro jump behind the target: prefer target velocity if available, otherwise use vector to target
                        Vector2 dirVel = Vector2.Zero;
                        if (TryComp<PhysicsComponent>(targetGrid.Value, out var targetBody))
                            dirVel = targetBody.LinearVelocity;
                        var dir = dirVel.LengthSquared() > 0.01f ? Vector2.Normalize(dirVel) : (toTarget == Vector2.Zero ? Vector2.UnitY : Vector2.Normalize(toTarget));
                        var arrive = targetPos - dir * brain.FtlExitOffset;
                        var coords = new MapCoordinates(arrive, xform.MapID);
                        // Arm post-FTL stabilization window
                        brain.StabilizeTimer = MathF.Max(brain.StabilizeTimer, brain.PostFtlStabilizeSeconds);
                        brain.FtlCooldownTimer = brain.FtlCooldownSeconds;
                        _shuttle.FTLToCoordinates(uid, Comp<Content.Server.Shuttles.Components.ShuttleComponent>(uid), _xform.ToCoordinates(coords), Angle.Zero);
                        continue;
                    }

                    // Orbit + band keep: tangential plus radial correction
                    var dirTo = dist > 0f ? toTarget / dist : Vector2.UnitX;
                    var tangential = new Vector2(-dirTo.Y, dirTo.X);
                    var radialError = ((brain.FightRangeMin + brain.FightRangeMax) * 0.5f) - dist;

                    // Obstacle avoidance via raycast: if blocked, bias desired motion tangentially away from hit normal
                    var desiredDir = tangential * 0.9f - dirTo * Math.Clamp(radialError * 0.5f, -1f, 1f);
                    var exclude = targetGrid;
                    var (avoidDir, forwardHit, leftClear, rightClear) = ComputeAvoidance(uid, pos, desiredDir, xform.MapID, exclude);
                    if (avoidDir != Vector2.Zero)
                    {
                        // Blend avoidance strongly to prevent collision; keep some goal seeking
                        desiredDir = Vector2.Normalize(avoidDir) * 1.2f + desiredDir * 0.8f;
                        // Rebuild components for DriveViaConsole using the blended direction
                        dirTo = desiredDir.LengthSquared() > 0.0001f ? Vector2.Normalize(desiredDir) : dirTo;
                        tangential = new Vector2(-dirTo.Y, dirTo.X);
                    }

                    // If something is very close ahead, force a strong back-off component
                    if (forwardHit > 0f && forwardHit < 12f)
                    {
                        desiredDir -= dirTo * 3.5f;
                        dirTo = desiredDir.LengthSquared() > 0.0001f ? Vector2.Normalize(desiredDir) : dirTo;
                        tangential = new Vector2(-dirTo.Y, dirTo.X);
                    }
                    // During stabilization, dampen radial correction to avoid oscillations
                    var radialForInput = radialError;
                    if (brain.StabilizeTimer > 0f)
                        radialForInput *= 0.35f;
                    else
                        radialForInput *= 1.2f;

                    // If an obstacle is very close ahead - do not push forward: pick the clearer side and apply braking
                    var requestBrake = false;
                    var sidePrefer = 0;
                    if (forwardHit > 0f && forwardHit < 16f)
                    {
                        var sideSign = leftClear >= rightClear ? 1f : -1f;
                        sidePrefer = sideSign >= 0f ? 1 : -1;
                        var side = new Vector2(-dirTo.Y, dirTo.X) * sideSign;
                        desiredDir = side - dirTo * 0.8f;
                        dirTo = desiredDir.LengthSquared() > 0.0001f ? Vector2.Normalize(desiredDir) : dirTo;
                        tangential = new Vector2(-dirTo.Y, dirTo.X);
                        radialForInput = MathF.Min(0f, radialForInput); // avoid forward pull when something is right in front of the nose
                        requestBrake = true;
                    }

                    // Decide input buttons from desired vector in shuttle local space (nose points to enemy)
                    DriveViaConsole(uid, brain, body, 1f / ControlHz, dirTo, tangential, radialForInput, requestBrake, sidePrefer);

                    // Aim at enemy console if known, else grid center
                    var fireAt = aimPos == default ? targetPos : aimPos;
                    // Do not fire beyond max weapon range to avoid wasting shots
                    if ((fireAt - pos).Length() <= brain.MaxWeaponRange + 1f)
                        _fire.TryAimAndFireGrid(uid, targetGrid.Value, fireAt);
                }
                else
                {
                    // Patrol: random waypoints with periodic straight-line FTL hops
                    var offset = pos - anchor;
                    var r = offset.Length();
                    var outward = offset == Vector2.Zero ? Vector2.UnitX : Vector2.Normalize(offset);

                    // 1) If there is no active patrol waypoint, generate one in a random direction
                    if (brain.PatrolWaypoint == null)
                    {
                        var rng = System.Random.Shared;
                        // Random planar direction (normalized)
                        var theta = (float)(rng.NextDouble() * Math.PI * 2.0);
                        var dirRnd = new Vector2(MathF.Cos(theta), MathF.Sin(theta));
                        var dist = Robust.Shared.Maths.MathHelper.Clamp(
                            (float)(brain.PatrolFtlMinDistance + rng.NextDouble() * (brain.PatrolFtlMaxDistance - brain.PatrolFtlMinDistance)),
                            brain.PatrolFtlMinDistance, brain.PatrolFtlMaxDistance);
                        brain.PatrolWaypoint = pos + dirRnd * dist;
                    }

                    // 2) If far from the waypoint, fly toward it; otherwise, perform an FTL hop to a new random waypoint
                    if (brain.PatrolWaypoint is { } wp)
                    {
                        var toWp = wp - pos;
                        var distWp = toWp.Length();
                        if (distWp > brain.PatrolWaypointTolerance)
                        {
                            var dirTo = distWp > 0f ? toWp / distWp : Vector2.UnitX;
                            var tangential = new Vector2(-dirTo.Y, dirTo.X);
                            brain.FacingMarker = wp; // face the waypoint to keep the nose aligned
                            DriveViaConsole(uid, brain, body, 1f / ControlHz, dirTo, tangential, 0f, false, 0);
                        }
                        else if (_shuttle.CanFTL(uid, out _))
                        {
                            // Straight-line FTL hop to a newly sampled waypoint
                            var rng = System.Random.Shared;
                            var theta = (float)(rng.NextDouble() * Math.PI * 2.0);
                            var dir = new Vector2(MathF.Cos(theta), MathF.Sin(theta));
                            var dist = Robust.Shared.Maths.MathHelper.Clamp(
                                (float)(brain.PatrolFtlMinDistance + rng.NextDouble() * (brain.PatrolFtlMaxDistance - brain.PatrolFtlMinDistance)),
                                brain.PatrolFtlMinDistance, brain.PatrolFtlMaxDistance);
                            var arrive = pos + dir * dist;
                            var coords = new MapCoordinates(arrive, xform.MapID);
                            brain.StabilizeTimer = MathF.Max(brain.StabilizeTimer, brain.PostFtlStabilizeSeconds);
                            _shuttle.FTLToCoordinates(uid, Comp<Content.Server.Shuttles.Components.ShuttleComponent>(uid), _xform.ToCoordinates(coords), Angle.Zero);
                            brain.PatrolWaypoint = null; // clear so a new waypoint will be generated after the jump
                        }
                        else
                        {
                            // If FTL is unavailable: apply a gentle tangential drift to avoid idling
                            var tangent = new Vector2(-outward.Y, outward.X);
                            DriveViaConsole(uid, brain, body, 1f / ControlHz, Vector2.Zero, tangent, 0f, false, 0);
                        }
                    }
                }
            }
        }
    }

    private void EnsurePilot(EntityUid grid, ref AiShuttleBrainComponent brain)
    {
        if (brain.PilotEntity is { } pilot && !Deleted(pilot) && TryComp<PilotComponent>(pilot, out var pilotComp) && pilotComp.Console != null)
            return;

        // Find a console on this grid to attach the pilot to
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
            Logger.DebugS("ai-shuttle", $"[{ToPrettyString(grid)}] EnsurePilot: no console found on grid");
            return;
        }

        // Spawn or reuse AI pilot and attach
        if (brain.PilotEntity == null || Deleted(brain.PilotEntity.Value))
        {
            // Prefer NF invisible pilot if available; fallback to the visible base pilot
            var proto = _prototypes.HasIndex<EntityPrototype>("MobShuttleAIPilotNF") ? "MobShuttleAIPilotNF" : "MobShuttleAIPilot";
            var spawn = Transform(consoleUid.Value).Coordinates;
            brain.PilotEntity = EntityManager.SpawnEntity(proto, spawn);
            Logger.DebugS("ai-shuttle", $"[{ToPrettyString(grid)}] EnsurePilot: spawned pilot {ToPrettyString(brain.PilotEntity.Value)} at console {ToPrettyString(consoleUid.Value)}");
        }

        var pilotEnt = brain.PilotEntity!.Value;
        EnsureComp<InputMoverComponent>(pilotEnt);
        EnsureComp<PilotComponent>(pilotEnt);
        // If already piloting this console, skip re-adding
        var p = Comp<PilotComponent>(pilotEnt);
        if (p.Console != consoleUid)
        {
            _console.AddPilot(consoleUid.Value, pilotEnt, Comp<ShuttleConsoleComponent>(consoleUid.Value));
            Logger.DebugS("ai-shuttle", $"[{ToPrettyString(grid)}] EnsurePilot: attached pilot {ToPrettyString(pilotEnt)} to console {ToPrettyString(consoleUid.Value)}");
        }
    }

    private void DriveViaConsole(EntityUid grid, AiShuttleBrainComponent brain, PhysicsComponent body, float controlDt,
        Vector2 dirToTarget, Vector2 tangential, float radialError, bool requestBrake, int sidePrefer)
    {
        if (brain.PilotEntity == null || !TryComp<PilotComponent>(brain.PilotEntity.Value, out var pilot))
            return;

        // Face nose toward target using rotate inputs
        var gridRot = _xform.GetWorldRotation(grid);
        var forwardBase = Angle.FromDegrees(brain.ForwardAngleOffset);
        var nose = (gridRot + forwardBase).RotateVec(Vector2.UnitY);

        // Determine facing vector: prefer internal FacingMarker if available
        var facingVec = dirToTarget;
        if (brain.FacingMarker is { } marker)
        {
            var gridXform = Transform(grid);
            var gridPos = _xform.GetWorldPosition(gridXform);
            var toMarker = marker - gridPos;
            if (toMarker.LengthSquared() > 0.0001f)
                facingVec = Vector2.Normalize(toMarker);
        }

        var cross = nose.X * facingVec.Y - nose.Y * facingVec.X;
        var dot = Vector2.Dot(nose, facingVec);
        var yawError = MathF.Atan2(cross, dot); // [-pi, pi], sign = turn dir needed

        // Build desired buttons and only update if changed to avoid net churn
        var desiredButtons = ShuttleButtons.None;

        // Rotation control tuned for gyro: PD gating + optional brake for smooth stop
        const float yawDeadzone = 0.02f;      // ~1.1 deg
        const float kAvPerRad = 3.0f;         // target av proportional to yaw error
        const float avEpsilon = 0.08f;        // near-enough angular velocity
        var av = body.AngularVelocity;
        var needTurn = MathF.Abs(yawError) > yawDeadzone;

        if (needTurn)
        {
            // Desired angular velocity bounded by ship limits
            var desiredAv = Math.Clamp(yawError * kAvPerRad, -ShuttleComponent.MaxAngularVelocity, ShuttleComponent.MaxAngularVelocity);
            var avErr = desiredAv - av;

            if (avErr > avEpsilon)
                desiredButtons |= ShuttleButtons.RotateLeft;   // increase +av (left)
            else if (avErr < -avEpsilon)
                desiredButtons |= ShuttleButtons.RotateRight;  // increase -av (right)
            // else: within band, let inertia carry
        }
        else
        {
            // Near aligned: let inertia settle; avoid global Brake which kills all linear axes
        }

        // Linear: combine tangential for orbit and radial to correct distance
        var desired = tangential * 0.9f + dirToTarget * Math.Clamp(radialError * 0.5f, -1f, 1f);
        // If we have a preferred sidestep, enforce lateral component to avoid "stuck in front of target" behavior
        if (sidePrefer != 0)
        {
            var side = new Vector2(-dirToTarget.Y, dirToTarget.X) * (sidePrefer > 0 ? 1f : -1f);
            desired += side * 0.8f;
        }
        // Convert desired into console-relative buttons (console local up = forward)
        var forward = (gridRot + forwardBase).RotateVec(Vector2.UnitY);
        var right = (gridRot + forwardBase).RotateVec(Vector2.UnitX);
        var fwdAmt = Vector2.Dot(desired, forward);
        var rightAmt = Vector2.Dot(desired, right);

        if (fwdAmt > 0.1f && !requestBrake)
            desiredButtons |= ShuttleButtons.StrafeUp;
        else if (fwdAmt < -0.1f)
            desiredButtons |= ShuttleButtons.StrafeDown;

        if (rightAmt > 0.1f)
            desiredButtons |= ShuttleButtons.StrafeRight;
        else if (rightAmt < -0.1f)
            desiredButtons |= ShuttleButtons.StrafeLeft;

        // Directional braking: if requested, cancel forward motion by applying reverse only if moving forward
        if (requestBrake)
        {
            var linVel = body.LinearVelocity;
            var fwdVel = Vector2.Dot(linVel, forward);
            if (fwdVel > 0.05f)
                desiredButtons |= ShuttleButtons.StrafeDown;
            // Forward thrust already blocked above by !requestBrake
        }

        if (pilot.HeldButtons != desiredButtons)
            pilot.HeldButtons = desiredButtons;
    }

    private Vector2 GetAnchorWorld(AiShuttleBrainComponent brain, MapId map)
    {
        if (!string.IsNullOrWhiteSpace(brain.AnchorEntityName))
        {
            var query = EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var meta, out var xform))
            {
                if (xform.MapID != map)
                    continue;
                if (meta.EntityName == brain.AnchorEntityName)
                    return _xform.GetWorldPosition(xform);
            }
        }
        return brain.FallbackAnchor;
    }

    /// <summary>
    /// Casts short-range rays in the desired movement direction to detect immediate obstacles.
    /// Returns an avoidance vector and distance to the closest forward hit (0 if none).
    /// </summary>
    private (Vector2 avoid, float forwardHit, float leftClear, float rightClear) ComputeAvoidance(EntityUid grid, Vector2 fromWorld, Vector2 desiredDir, MapId mapId, EntityUid? exclude)
    {
        if (desiredDir == Vector2.Zero)
            return (Vector2.Zero, 0f, 0f, 0f);

        var dir = Vector2.Normalize(desiredDir);
        const float probeDist = 36f; // meters ahead to probe (increased)
        const float sideProbeDist = 22f;
        var collisionMask = (int)(CollisionGroup.Impassable | CollisionGroup.Opaque);

        (Vector2 sum, float minDist, float freeDist) AccumulateAvoid(Vector2 start, Vector2 direction, float dist)
        {
            var ray = new Robust.Shared.Physics.CollisionRay(start, direction, collisionMask);
            // Ignore own grid entity to not detect self
            var hits = _physics.IntersectRay(mapId, ray, dist, grid, returnOnFirstHit: false);
            Vector2 sum = Vector2.Zero;
            float min = float.MaxValue;
            float maxFree = dist;
            foreach (var hit in hits)
            {
                if (exclude != null)
                {
                    if (hit.HitEntity == exclude)
                        continue;
                    if (TryComp<TransformComponent>(hit.HitEntity, out var hx) && hx.GridUid == exclude)
                        continue;
                }
                // Push away from hit point back along direction
                var away = start - hit.HitPos;
                if (away.LengthSquared() > 0.0001f)
                    sum += Vector2.Normalize(away);
                if (hit.Distance > 0f && hit.Distance < min)
                    min = hit.Distance;
                if (hit.Distance > 0f && hit.Distance < maxFree)
                    maxFree = hit.Distance;
            }
            return (sum, hits.Any() ? min : 0f, maxFree);
        }
        // Offset sensors slightly towards the nose so we react before nose contact
        var noseStart = fromWorld + dir * 5.5f;
        var ahead = AccumulateAvoid(noseStart, dir, probeDist);
        var leftDir = new Vector2(-dir.Y, dir.X);
        var rightDir = -leftDir;
        var left = AccumulateAvoid(noseStart, leftDir, sideProbeDist);
        var right = AccumulateAvoid(noseStart, rightDir, sideProbeDist);
        var aheadLeft = AccumulateAvoid(noseStart, Vector2.Normalize(dir + leftDir * 0.6f), probeDist * 0.8f);
        var aheadRight = AccumulateAvoid(noseStart, Vector2.Normalize(dir + rightDir * 0.6f), probeDist * 0.8f);

        var avoid = ahead.sum * 2.2f + (aheadLeft.sum + aheadRight.sum) * 1.2f + left.sum * 1.0f + right.sum * 1.0f;
        return (avoid, ahead.minDist, left.freeDist, right.freeDist);
    }

    private bool TryGetEnemyShuttleConsolePos(EntityUid enemyGrid, out Vector2 worldPos)
    {
        var q = EntityQueryEnumerator<ShuttleConsoleComponent, TransformComponent>();
        while (q.MoveNext(out var consoleUid, out _, out var cxform))
        {
            if (cxform.GridUid == enemyGrid)
            {
                worldPos = _xform.GetWorldPosition(cxform);
                return true;
            }
        }
        worldPos = default;
        return false;
    }

    /// <summary>
    /// Casts a ray from our position towards targetWorld and returns distance to the first solid collider
    /// along that path (walls, hull, floors with collision). Skips our own grid. If nothing hit, returns 0.
    /// </summary>
    private float ComputeHullDistance(EntityUid selfGrid, Vector2 fromWorld, Vector2 targetWorld, MapId mapId, EntityUid targetGrid)
    {
        var delta = targetWorld - fromWorld;
        var maxDist = delta.Length();
        if (maxDist <= 0f)
            return 0f;

        var dir = delta / maxDist;
        var mask = (int)(CollisionGroup.Impassable | CollisionGroup.Opaque);
        var ray = new Robust.Shared.Physics.CollisionRay(fromWorld, dir, mask);
        var hits = _physics.IntersectRay(mapId, ray, maxDist, selfGrid, returnOnFirstHit: false);

        float nearest = float.MaxValue;
        foreach (var hit in hits)
        {
            // Skip own grid fixtures
            if (hit.HitEntity == selfGrid)
                continue;
            if (TryComp<TransformComponent>(hit.HitEntity, out var hx) && hx.GridUid == selfGrid)
                continue;

            if (hit.Distance > 0f && hit.Distance < nearest)
                nearest = hit.Distance;
        }
        return nearest == float.MaxValue ? 0f : nearest;
    }

    // private void AssignPerunWings(MapId mapId)
    // {
    //     // Gather all grids named exactly "Perun" on the given map
    //     var peruns = new List<(EntityUid uid, AiShuttleBrainComponent brain, TransformComponent xform)>();
    //     var q = EntityQueryEnumerator<AiShuttleBrainComponent, TransformComponent>();
    //     while (q.MoveNext(out var uid, out var brain, out var xform))
    //     {
    //         if (xform.MapID != mapId)
    //             continue;
    //         if (!TryComp<MetaDataComponent>(uid, out var meta))
    //             continue;
    //         if (!string.Equals(meta.EntityName, "Perun", StringComparison.Ordinal))
    //             continue;
    //         peruns.Add((uid, brain, xform));
    //     }
    //
    //     // Reset any previous wing roles
    //     foreach (var (uid, brain, _) in peruns)
    //     {
    //         brain.WingLeader = null;
    //         brain.WingSlot = -1;
    //     }
    //
    //     // Group into triplets in discovery order
    //     for (int i = 0; i + 2 < peruns.Count; i += 3)
    //     {
    //         var a = peruns[i];
    //         var b = peruns[i + 1];
    //         var c = peruns[i + 2];
    //
    //         // Leader = first in the triplet
    //         var leader = a.uid;
    //         var leaderBrain = a.brain;
    //         leaderBrain.WingLeader = leader; // leader references itself
    //         leaderBrain.WingSlot = 0;
    //
    //         // Wingmen = second and third
    //         b.brain.WingLeader = leader;
    //         b.brain.WingSlot = 1; // left slot
    //         c.brain.WingLeader = leader;
    //         c.brain.WingSlot = 2; // right slot
    //     }
    // }
}
