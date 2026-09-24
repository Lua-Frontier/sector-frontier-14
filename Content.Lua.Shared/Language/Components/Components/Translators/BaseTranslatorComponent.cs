using Content.Shared.Language;
using Robust.Shared.Prototypes;

namespace Content.Lua.Shared.Language.Components.Translators;

public abstract partial class BaseTranslatorComponent : Component
{
    [DataField("spoken")]
    public List<ProtoId<LanguagePrototype>> SpokenLanguages = new();

    [DataField("understood")]
    public List<ProtoId<LanguagePrototype>> UnderstoodLanguages = new();

    [DataField("requires")]
    public List<ProtoId<LanguagePrototype>> RequiredLanguages = new();

    [DataField("requiresAll"), ViewVariables(VVAccess.ReadWrite)]
    public bool RequiresAllLanguages = false;

    [DataField("enabled"), ViewVariables(VVAccess.ReadWrite)]
    public bool Enabled = true;
}
