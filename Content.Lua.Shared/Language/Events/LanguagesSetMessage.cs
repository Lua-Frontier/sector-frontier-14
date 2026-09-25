using Robust.Shared.Serialization;
using Content.Shared.Language;

namespace Content.Lua.Shared.Language.Events;

[Serializable, NetSerializable]
public sealed class LanguagesSetMessage(string currentLanguage) : EntityEventArgs
{
    public string CurrentLanguage = currentLanguage;
}


