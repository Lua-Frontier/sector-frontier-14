using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Lua.Shared.Fulton;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FultonComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField("applyDuration"), AutoNetworkedField]
    public TimeSpan ApplyFultonDuration = TimeSpan.FromSeconds(3);

    [ViewVariables(VVAccess.ReadWrite), DataField("beacon"), AutoNetworkedField]
    public EntityUid? Beacon;

    [ViewVariables(VVAccess.ReadWrite), DataField("removeable"), AutoNetworkedField]
    public bool Removeable = true;

    [ViewVariables(VVAccess.ReadWrite), DataField("duration")]
    public TimeSpan FultonDuration = TimeSpan.FromSeconds(45);

    [ViewVariables(VVAccess.ReadWrite), DataField("whitelist"), AutoNetworkedField]
    public EntityWhitelist? Whitelist = new()
    {
        Components = new[]
        {
            "Item",
            "Anchorable"
        }
    };

    [ViewVariables(VVAccess.ReadWrite), DataField("soundFulton"), AutoNetworkedField]
    public SoundSpecifier? FultonSound = new SoundPathSpecifier("/Audio/Items/Mining/fultext_deploy.ogg");
}
