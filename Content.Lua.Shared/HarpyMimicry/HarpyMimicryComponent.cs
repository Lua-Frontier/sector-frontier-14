using Content.Shared.Chat.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Lua.Shared.HarpyMimicry;

[RegisterComponent, NetworkedComponent]
public sealed partial class HarpyMimicryComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<EmotePrototype>, List<HarpyMimicSound>> Sounds = new();

    [ViewVariables]
    public (SoundSpecifier Sound, AudioParams Params)? Pending;
}

[DataDefinition]
public sealed partial class HarpyMimicSound
{
    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public EntProtoId Icon;

    [DataField(required: true)]
    public SoundSpecifier Sound = default!;
}
