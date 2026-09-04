using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Explosion;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class RMCExplosionShockWaveComponent : Component
{
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float FalloffPower = 40.0f;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float Sharpness = 10.0f;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float Width = 0.8f;

    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan StartTime;
}
