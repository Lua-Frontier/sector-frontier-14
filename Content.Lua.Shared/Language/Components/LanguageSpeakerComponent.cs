using Robust.Shared.Prototypes;
using Content.Shared.Language;

namespace Content.Lua.Shared.Language;

[RegisterComponent]
public sealed partial class LanguageSpeakerComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public ProtoId<LanguagePrototype>? CurrentLanguage;

    public List<ProtoId<LanguagePrototype>> SpokenLanguages = [];
    public List<ProtoId<LanguagePrototype>> UnderstoodLanguages = [];
}


