using Content.Shared.Chat.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Lua.Shared.Emotes;

[RegisterComponent, NetworkedComponent]
public sealed partial class EmoteIconOverrideComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<EmotePrototype>, SpriteSpecifier> Icons = new();
}
