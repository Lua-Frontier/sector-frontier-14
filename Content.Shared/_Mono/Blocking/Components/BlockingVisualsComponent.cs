using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Blocking.Components;

/// <summary>
/// This component gets dynamically added to an Entity via the <see cref="BlockingSystem"/> if the IsClothing is true
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class BlockingVisualsComponent : Component
{
    [DataField("enabled")]
    [AutoNetworkedField]
    public bool Enabled = true;
}
