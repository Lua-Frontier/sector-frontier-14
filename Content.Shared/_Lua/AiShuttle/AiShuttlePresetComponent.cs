using System.Numerics;
using Robust.Shared.GameStates;
// Lua: Networked preset for shuttle AI. Attach to a grid to override
// the default brain parameters without changing prototypes at large.

namespace Content.Shared._Mono.AiShuttle;

/// <summary>
/// Lua: Preset of AI brain values applied to the owning grid at runtime.
/// </summary>
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
}

