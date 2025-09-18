using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;

namespace Content.Shared._Lua.Language.Components.Translators;

public abstract partial class BaseTranslatorComponent : Component
{
    [DataField("spoken", customTypeSerializer: typeof(PrototypeIdListSerializer<LanguagePrototype>))]
    public List<string> SpokenLanguages = new();

    [DataField("understood", customTypeSerializer: typeof(PrototypeIdListSerializer<LanguagePrototype>))]
    public List<string> UnderstoodLanguages = new();

    [DataField("requires", customTypeSerializer: typeof(PrototypeIdListSerializer<LanguagePrototype>))]
    public List<string> RequiredLanguages = new();

    [DataField("requiresAll"), ViewVariables(VVAccess.ReadWrite)]
    public bool RequiresAllLanguages = false;

    [DataField("enabled"), ViewVariables(VVAccess.ReadWrite)]
    public bool Enabled = true;
}


