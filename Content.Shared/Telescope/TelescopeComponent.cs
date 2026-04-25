using Robust.Shared.GameStates;

namespace Content.Shared.Telescope;

[RegisterComponent, NetworkedComponent]
public sealed partial class TelescopeComponent : Component
{
    [DataField]
    public float Divisor = 0.1f;

    [DataField]
    public float LerpAmount = 0.1f;
}
