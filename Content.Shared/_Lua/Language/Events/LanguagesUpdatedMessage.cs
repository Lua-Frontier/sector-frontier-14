using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Language.Events;

[Serializable, NetSerializable]
public sealed class LanguagesUpdatedMessage(ProtoId<LanguagePrototype> currentLanguage, List<ProtoId<LanguagePrototype>> spoken, List<ProtoId<LanguagePrototype>> understood) : EntityEventArgs
{
    public ProtoId<LanguagePrototype> CurrentLanguage = currentLanguage;
    public List<ProtoId<LanguagePrototype>> Spoken = spoken;
    public List<ProtoId<LanguagePrototype>> Understood = understood;
}


