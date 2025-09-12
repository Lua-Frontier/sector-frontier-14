// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.
using System.Numerics;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lua.AiShuttle;

[Prototype("AiShuttlePreset")]
public sealed partial class AiShuttlePresetPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField] public string? AnchorEntityName;
    [DataField] public Vector2 FallbackAnchor = Vector2.Zero;
    [DataField] public float MinPatrolRadius = 150f;
    [DataField] public float MaxPatrolRadius = 1500f;
    [DataField] public float FightRangeMin = 160f;
    [DataField] public float FightRangeMax = 220f;
    [DataField] public float FtlMinDistance = 400f;
    [DataField] public float FtlExitOffset = 180f;
    [DataField] public float PostFtlStabilizeSeconds = 2.5f;
    [DataField] public float FtlCooldownSeconds = 320f;
    [DataField] public float RetreatHealthFraction = 0.3f;
    [DataField] public bool AvoidStationFire = true;
    [DataField] public bool ForbidRamming = true;
    [DataField] public bool TargetShuttlesOnly = true;
    [DataField] public bool TargetShuttles = true;
    [DataField] public bool TargetDebris = false;
    [DataField] public float MinSeparation = 50f;
    [DataField] public float MaxWeaponRange = 512f;
    [DataField] public float ForwardAngleOffset = 0f;
    [DataField] public bool? PatrolSectorEnabled;
    [DataField] public Vector2? PatrolSectorCenter;
    [DataField] public float? PatrolSectorRadius;
    [DataField] public float? PatrolHoldSeconds;
    [DataField] public float? YawDeadzoneDegrees;
    [DataField] public float? YawGateDegrees;
    [DataField] public float? YawKAvPerRad;
    [DataField] public float? YawAvEpsilon;
    [DataField] public float? YawAvBrakeThreshold;
    [DataField] public bool? KeepFacingOnHold;
    [DataField] public float? OrbitTangentialScale;
}


