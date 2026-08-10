using Content.Shared._RMC14.Wieldable;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Wieldable.Components;

/// <summary>
/// Applies a UseDelay when a gun/item is equipped or selected in hand,
/// and optionally blocks firing while that delay is active.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(RMCWieldableSystem))]
public sealed partial class WieldDelayComponent : Component
{
    /// <summary>
    /// Base delay applied on equip / hand-select / wield.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan BaseDelay = TimeSpan.FromSeconds(0.75);

    [DataField, AutoNetworkedField]
    public TimeSpan ModifiedDelay = TimeSpan.FromSeconds(0.75);

    /// <summary>
    /// If true, shooting is blocked while the draw/wield delay is active.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool PreventFiring = true;
}
