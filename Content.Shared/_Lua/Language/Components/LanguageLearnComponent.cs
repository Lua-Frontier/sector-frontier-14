using Robust.Shared.Audio;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;

namespace Content.Shared._Lua.Language.Components;

[RegisterComponent]
public sealed partial class LanguageLearnComponent : Component
{
    [DataField(required: true, customTypeSerializer: typeof(PrototypeIdListSerializer<LanguagePrototype>))]
    public List<string> Languages { get; set; } = new List<string>();

    [DataField]
    public float DoAfterDuration = 3f;

    [DataField]
    public SoundSpecifier? UseSound = new SoundPathSpecifier("/Audio/Items/Paper/paper_scribble1.ogg");

    [DataField]
    public int MaxUses = 1;

    [DataField]
    public bool DeleteAfterUse = false;

    [ViewVariables]
    public int? UsesRemaining = null;

    public int GetUsesRemaining()
    {
        return UsesRemaining ?? MaxUses;
    }
}


