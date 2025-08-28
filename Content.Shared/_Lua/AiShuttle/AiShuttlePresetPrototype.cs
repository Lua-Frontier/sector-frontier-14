using System.Numerics;
using Robust.Shared.Prototypes;
// Lua: AI brain preset prototype. Can be referenced by ID or auto-matched
// by exact grid name (MetaData.EntityName) via NameMatch.

namespace Content.Shared._Mono.AiShuttle;

/// <summary>
/// Lua: AI brain preset prototype. Supports optional matching by exact grid name.
/// </summary>
[Prototype("AiShuttlePreset")]
public sealed partial class AiShuttlePresetPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    /// <summary>
    /// Optional exact grid name to match against MetaData.EntityName (e.g. CR-GF "Perun").
    /// </summary>
    [DataField]
    public string? NameMatch;

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
}


