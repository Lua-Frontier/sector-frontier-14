using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._Lua.AiShuttle;

[RegisterComponent, NetworkedComponent]
public sealed partial class AiShuttlePresetComponent : Component
{
    [DataField]
    public string? AnchorEntityName;

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
    public float FtlExitOffset = 185f;

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
    public float MinSeparation = 50f;

    [DataField]
    public float MaxWeaponRange = 512f;

    [DataField]
    public float ForwardAngleOffset = 0f;

    [DataField]
    public bool? PatrolSectorEnabled;

    [DataField]
    public Vector2? PatrolSectorCenter;

    [DataField]
    public float? PatrolSectorRadius;

    [DataField]
    public float? PatrolHoldSeconds;

    [DataField] public float? YawDeadzoneDegrees;
    [DataField] public float? YawGateDegrees;
    [DataField] public float? YawKAvPerRad;
    [DataField] public float? YawAvEpsilon;
    [DataField] public float? YawAvBrakeThreshold;
    [DataField] public bool? KeepFacingOnHold;
    [DataField] public float? OrbitTangentialScale;
}

