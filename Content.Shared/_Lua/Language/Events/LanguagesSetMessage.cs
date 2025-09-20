using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Language.Events;

[Serializable, NetSerializable]
public sealed class LanguagesSetMessage(string currentLanguage) : EntityEventArgs
{
    public string CurrentLanguage = currentLanguage;
}


