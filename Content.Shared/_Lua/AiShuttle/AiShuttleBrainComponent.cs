using System.Numerics;
// Lua: Common AI brain configuration for autopiloted shuttle grids.
// This component holds tuning parameters consumed by the server-side brain system
// to orchestrate patrol, target selection, maneuvering, FTL micro-jumps and weapon usage.

namespace Content.Shared._Mono.AiShuttle;

/// <summary>
/// Lua: Configuration container for shuttle AI. Stores engagement ranges,
/// FTL behavior, patrol settings, targeting toggles, and runtime state used by the server system.
/// </summary>
[RegisterComponent]
public sealed partial class AiShuttleBrainComponent : Component
{
    // Patrol anchoring: defines the ring center this shuttle orbits around while idle
    [DataField]
    public string? AnchorEntityName;

    [DataField]
    public Vector2 FallbackAnchor = Vector2.Zero;

    [DataField]
    public float MinPatrolRadius = 150f;

    [DataField]
    public float MaxPatrolRadius = 1500f;

    // Desired combat distance window (meters) while orbiting a target
    [DataField]
    public float FightRangeMin = 160f;

    [DataField]
    public float FightRangeMax = 220f;

    // FTL behavior: thresholds and offsets controlling micro-jumps used for repositioning
    [DataField]
    public float FtlMinDistance = 400f;

    [DataField]
    public float FtlExitOffset = 180f;

    // Post-FTL stabilization: dampens inputs for a short period after a jump to avoid oscillations
    [DataField]
    public float PostFtlStabilizeSeconds = 2.5f;

    // FTL cooldown (seconds) to avoid frequent micro-jumps
    [DataField]
    public float FtlCooldownSeconds = 320f;

    // Safety / retreat tuning
    [DataField]
    public float RetreatHealthFraction = 0.3f;

    [DataField]
    public bool AvoidStationFire = true;

    [DataField]
    public bool ForbidRamming = true;

    // Target filtering
    [DataField]
    public bool TargetShuttlesOnly = true;

    // Explicit target toggles (override-only). If both are false, falls back to TargetShuttlesOnly.
    [DataField]
    public bool TargetShuttles = true;

    [DataField]
    public bool TargetDebris = false;

    [DataField]
    public bool Enabled = true;

    // Minimum allowed separation to the target (meters). Enforced to prevent ramming.
    [DataField]
    public float MinSeparation = 50f;

    // Max effective weapon engagement distance (meters). Used to avoid wasting shots.
    [DataField]
    public float MaxWeaponRange = 512f;

    // Distance threshold for switching from patrol to combat when a valid enemy is present
    [DataField]
    public float CombatTriggerRadius = 512f;

    // Optional forward-basis tweak in degrees to align the definition of the grid "nose"
    [DataField]
    public float ForwardAngleOffset = 0f;

    // Patrol FTL hop distance range (meters) used for random idle jumps
    [DataField]
    public float PatrolFtlMinDistance = 450f;

    [DataField]
    public float PatrolFtlMaxDistance = 1250f;

    // Runtime state (server-only)
    [ViewVariables, DataField(serverOnly: true)]
    public EntityUid? CurrentTarget;

    [ViewVariables, DataField(serverOnly: true)]
    public Vector2? LastKnownTargetPos;

    [ViewVariables, DataField(serverOnly: true)]
    public EntityUid? PilotEntity;

    // Current random patrol waypoint (world position) when out of combat
    [ViewVariables, DataField(serverOnly: true)]
    public Vector2? PatrolWaypoint;

    // Tolerance for considering a patrol waypoint reached
    [DataField]
    public float PatrolWaypointTolerance = 60f;

    // Formation / wing (disabled)
    // [DataField]
    // public float FormationSpacing = 45f; // target spacing between ships inside a wing

    // Server-only wing state (disabled)
    // [ViewVariables, DataField(serverOnly: true)]
    // public EntityUid? WingLeader; // null or self => this grid is the leader

    // [ViewVariables, DataField(serverOnly: true)]
    // public int WingSlot; // 0 = leader, 1 = left wingman, 2 = right wingman; -1 = no wing

    // Whether the AI is actively engaging the current target (combat mode)
    [ViewVariables, DataField(serverOnly: true)]
    public bool InCombat;

    // Internal facing marker (world position). If set, the nose will try to face this point.
    [ViewVariables, DataField(serverOnly: true)]
    public Vector2? FacingMarker;

    // Server-only countdown to dampen inputs right after FTL to avoid oscillation
    [ViewVariables, DataField(serverOnly: true)]
    public float StabilizeTimer;

    // Server-only countdown for FTL cooldown
    [ViewVariables, DataField(serverOnly: true)]
    public float FtlCooldownTimer;
}


