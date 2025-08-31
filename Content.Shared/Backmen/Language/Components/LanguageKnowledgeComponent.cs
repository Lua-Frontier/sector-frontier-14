using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;

namespace Content.Shared.Backmen.Language.Components;

[RegisterComponent]
public sealed partial class LanguageKnowledgeComponent : Component
{
    [DataField("speaks", customTypeSerializer: typeof(PrototypeIdListSerializer<LanguagePrototype>), required: true)]
    public List<string> SpokenLanguages = new();

    [DataField("understands", customTypeSerializer: typeof(PrototypeIdListSerializer<LanguagePrototype>), required: true)]
    public List<string> UnderstoodLanguages = new();
}


