using Robust.Shared.Audio;

namespace Content.Server._EinsteinEngines.Silicon.Charge;

/// <summary>
/// Marks a powered entity as a valid battery source for IPC/silicon battery drinkers.
/// </summary>
[RegisterComponent]
public sealed partial class BatteryDrinkerSourceComponent : Component
{
    /// <summary>
    /// The max amount of power this source can provide in one sip.
    /// No limit if null.
    /// </summary>
    [DataField]
    public int? MaxAmount = null;

    /// <summary>
    /// The multiplier for the drink speed.
    /// </summary>
    [DataField]
    public float DrinkSpeedMulti = 1f;

    /// <summary>
    /// The sound to play when the battery gets drunk from.
    /// </summary>
    [DataField]
    public SoundSpecifier? DrinkSound = new SoundCollectionSpecifier("sparks");
}
