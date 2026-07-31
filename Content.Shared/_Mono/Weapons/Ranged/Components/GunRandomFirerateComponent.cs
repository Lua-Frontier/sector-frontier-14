using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Weapons.Ranged.Components;

/// <summary>
/// Applies a random multiplier to time until next shot every time this gun fires.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GunRandomFirerateComponent : Component
{
    /// <summary>
    /// If true, rolls a reload-time multiplier. If false, rolls a firerate multiplier (inverted).
    /// </summary>
    [DataField]
    public bool AsTime = true;

    [DataField(required: true)]
    public float MinMul = 1f;

    [DataField(required: true)]
    public float MaxMul = 1f;
}
