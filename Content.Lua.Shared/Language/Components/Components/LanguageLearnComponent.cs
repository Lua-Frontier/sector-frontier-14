using Robust.Shared.Audio;
using Content.Shared.Language;
using Robust.Shared.Prototypes;

namespace Content.Lua.Shared.Language.Components;

[RegisterComponent]
public sealed partial class LanguageLearnComponent : Component
{
    [DataField(required: true)]
    public List<ProtoId<LanguagePrototype>> Languages { get; set; } = new();

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


