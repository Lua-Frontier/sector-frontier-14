// Copyright Rane (elijahrane@gmail.com) 2025
// All rights reserved. Relicensed under AGPL with permission

using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Mono.FireControl;

[RegisterComponent]
public sealed partial class FireControllableComponent : Component
{
    /// <summary>
    /// Reference to the controlling server, if any.
    /// </summary>
    [ViewVariables]
    public EntityUid? ControllingServer = null;

    /// <summary>
    /// When the weapon can next be fired
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextFire = TimeSpan.Zero;

    /// <summary>
    /// Cooldown between firing, in seconds
    /// </summary>
    [DataField]
    public float FireCooldown = 0.2f;
	//Lua start:
    /// <summary>
    /// Max allowed angle (in degrees) between weapon's forward and target direction to allow AI fire.
    /// Only used by AI firing helper; player console fire is unaffected.
    /// </summary>
    [DataField]
    public float FireArcDegrees = 180f;

    /// <summary>
    /// If true, also allow AI fire when target is within this arc relative to the grid's nose.
    /// </summary>
    [DataField]
    public bool UseGridNoseArc = true;

    /// <summary>
    /// Max allowed angle (in degrees) from grid nose to target to allow AI fire.
    /// </summary>
    [DataField]
    public float GridNoseArcDegrees = 75f;//Lua end
}
