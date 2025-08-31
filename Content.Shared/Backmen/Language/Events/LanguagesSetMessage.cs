using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Language.Events;

[Serializable, NetSerializable]
public sealed class LanguagesSetMessage(string currentLanguage) : EntityEventArgs
{
    public string CurrentLanguage = currentLanguage;
}


