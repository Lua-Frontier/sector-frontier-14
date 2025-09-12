// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.
using System.Numerics;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lua.AiShuttle;

[RegisterComponent]
public sealed partial class AiShuttleBrainComponent : Component
{
    [DataField]
    public string? AnchorEntityName;

    [DataField]
    public ProtoId<AiShuttlePresetPrototype>? Preset = "Default";

    [DataField]
    public Vector2 FallbackAnchor = Vector2.Zero;

    [DataField]
    public float MinPatrolRadius = 150f;

    [DataField]
    public float MaxPatrolRadius = 1500f;

    [DataField]
    public float FightRangeMin = 160f;

    [DataField]
    public float FightRangeMax = 220f;

    [DataField]
    public float FtlMinDistance = 400f;

    [DataField]
    public float FtlExitOffset = 180f;

    [DataField]
    public float PostFtlStabilizeSeconds = 2.5f;

    [DataField]
    public float FtlCooldownSeconds = 320f;

    [DataField]
    public float RetreatHealthFraction = 0.3f;

    [DataField]
    public bool AvoidStationFire = true;

    [DataField]
    public bool ForbidRamming = true;

    [DataField]
    public bool TargetShuttlesOnly = true;

    [DataField]
    public bool TargetShuttles = true;

    [DataField]
    public bool TargetDebris = false;

    [DataField]
    public bool Enabled = true;

    [DataField]
    public float MinSeparation = 50f;

    [DataField]
    public float MaxWeaponRange = 512f;

    [DataField]
    public float CombatTriggerRadius = 512f;

    [DataField]
    public float ForwardAngleOffset = 0f;

    [DataField]
    public float PatrolFtlMinDistance = 450f;

    [DataField]
    public float PatrolFtlMaxDistance = 1250f;

    [DataField]
    public bool PatrolSectorEnabled = true;

    [DataField]
    public Vector2 PatrolSectorCenter = Vector2.Zero;

    [DataField]
    public float PatrolSectorRadius = 5000f;

    [DataField]
    public float PatrolHoldSeconds = 30f;

    [ViewVariables, DataField(serverOnly: true)]
    public EntityUid? CurrentTarget;

    [ViewVariables, DataField(serverOnly: true)]
    public Vector2? LastKnownTargetPos;

    [ViewVariables, DataField(serverOnly: true)]
    public EntityUid? PilotEntity;

    [ViewVariables, DataField(serverOnly: true)]
    public Vector2? PatrolWaypoint;

    [ViewVariables, DataField(serverOnly: true)]
    public float PatrolHoldTimer;

    [ViewVariables, DataField(serverOnly: true)]
    public bool PatrolHolding;

    [DataField]
    public float YawDeadzoneDegrees = 2.5f;

    [DataField]
    public float YawGateDegrees = 7.0f;

    [DataField]
    public float YawKAvPerRad = 2.2f;

    [DataField]
    public float YawAvEpsilon = 0.12f;

    [DataField]
    public float YawAvBrakeThreshold = 0.06f;

    [DataField]
    public bool KeepFacingOnHold = false;

    [DataField]
    public float OrbitTangentialScale = 0.6f;

    [DataField]
    public float ObstacleForwardStopDistance = 114f;

    [DataField]
    public float ObstacleStopVelocityMultiplier = 3.0f;

    [DataField]
    public float PatrolBlockedResetSeconds = 300f;

    [ViewVariables, DataField(serverOnly: true)]
    public float PatrolBlockedTimer;

    [ViewVariables, DataField(serverOnly: true)]
    public float PatrolLastDistToWaypoint;

    [DataField]
    public float PatrolWaypointTolerance = 60f;

    [DataField]
    public float FormationSpacing = 45f;

    [ViewVariables, DataField(serverOnly: true)]
    public EntityUid? WingLeader;

    [ViewVariables, DataField(serverOnly: true)]
    public int WingSlot = -1;

    [ViewVariables, DataField(serverOnly: true)]
    public bool FormingWing = false;

    [ViewVariables, DataField(serverOnly: true)]
    public bool InCombat;

    [ViewVariables, DataField(serverOnly: true)]
    public Vector2? FacingMarker;

    [ViewVariables, DataField(serverOnly: true)]
    public float StabilizeTimer;

    [ViewVariables, DataField(serverOnly: true)]
    public float FtlCooldownTimer;

    [DataField]
    public float ConsoleRetryIntervalSeconds = 30f;

    [ViewVariables, DataField(serverOnly: true)]
    public float ConsoleRetryTimer;

    [ViewVariables, DataField(serverOnly: true)]
    public bool WaitingForConsole;

    [DataField]
    public bool RespectFtlExclusions = true;

    [DataField]
    public float FtlExclusionBuffer = 64f;

    [DataField]
    public bool AttackArmedCiviliansInExclusions = true;
}


