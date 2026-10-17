using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Arcane.ERP;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class OrgasmWeaknessComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ExpiresAt;

    [DataField, AutoNetworkedField]
    public float SpeedModifier = 0.5f;

    [DataField]
    public TimeSpan WeaknessDuration = TimeSpan.FromSeconds(5);
}
