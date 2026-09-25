using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared.Language;

namespace Content.Lua.Shared.Language.Events;

[Serializable, NetSerializable]
public sealed class LanguagesUpdatedMessage(ProtoId<LanguagePrototype> currentLanguage, List<ProtoId<LanguagePrototype>> spoken, List<ProtoId<LanguagePrototype>> understood) : EntityEventArgs
{
    public ProtoId<LanguagePrototype> CurrentLanguage = currentLanguage;
    public List<ProtoId<LanguagePrototype>> Spoken = spoken;
    public List<ProtoId<LanguagePrototype>> Understood = understood;
}


