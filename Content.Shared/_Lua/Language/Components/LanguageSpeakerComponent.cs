using Robust.Shared.Prototypes;

namespace Content.Shared._Lua.Language;

[RegisterComponent]
public sealed partial class LanguageSpeakerComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public ProtoId<LanguagePrototype>? CurrentLanguage;

    public List<ProtoId<LanguagePrototype>> SpokenLanguages = [];
    public List<ProtoId<LanguagePrototype>> UnderstoodLanguages = [];
}


