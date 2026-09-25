using Content.Lua.Shared.Language;
using Content.Lua.Shared.Language.Components;
using Robust.Shared.GameObjects;
using Content.Shared.Language;

namespace Content.Lua.Shared.Language;

public interface ILanguageSystem : IEntitySystem
{
    LanguagePrototype GetLanguage(Entity<LanguageSpeakerComponent?> speaker);
    bool CanUnderstand(Entity<LanguageSpeakerComponent?> listener, string language);
    string ObfuscateSpeech(string message, LanguagePrototype language);
}
