using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Lua.Shared.Fulton;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
public sealed partial class FultonedComponent : Component
{
    [ViewVariables, DataField("effect"), AutoNetworkedField]
    public EntityUid Effect { get; set; }

    [ViewVariables(VVAccess.ReadWrite), DataField("beacon")]
    public EntityUid? Beacon;

    [ViewVariables(VVAccess.ReadWrite), DataField("fultonDuration"), AutoNetworkedField]
    public TimeSpan FultonDuration = TimeSpan.FromSeconds(45);

    [ViewVariables(VVAccess.ReadWrite), DataField("nextFulton", customTypeSerializer:typeof(TimeOffsetSerializer)), AutoNetworkedField]
    [AutoPausedField]
    public TimeSpan NextFulton;

    [ViewVariables(VVAccess.ReadWrite), DataField("sound"), AutoNetworkedField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Items/Mining/fultext_launch.ogg");

    [ViewVariables(VVAccess.ReadWrite), DataField("removeable")]
    public bool Removeable = true;
}
