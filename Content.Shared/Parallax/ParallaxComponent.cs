using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Parallax;

/// <summary>
/// Handles per-map parallax.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ParallaxComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<ParallaxPrototype> Parallax = "Default";
}
