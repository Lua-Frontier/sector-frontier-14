using Content.Shared.Language;
using Robust.Shared.Prototypes;

namespace Content.Lua.Shared.Language.Components;

[RegisterComponent]
public sealed partial class LanguageKnowledgeComponent : Component
{
    [DataField("speaks", required: true)]
    public List<ProtoId<LanguagePrototype>> SpokenLanguages = new();

    [DataField("understands", required: true)]
    public List<ProtoId<LanguagePrototype>> UnderstoodLanguages = new();
}
