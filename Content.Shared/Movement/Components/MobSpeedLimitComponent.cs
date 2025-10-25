using Robust.Shared.GameStates;

namespace Content.Shared.Movement.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MobSpeedLimitComponent : Component
{
    [DataField, AutoNetworkedField]
    public float MaxLinearVelocity = 25f;

    [DataField, AutoNetworkedField]
    public bool Enabled = true;
}
