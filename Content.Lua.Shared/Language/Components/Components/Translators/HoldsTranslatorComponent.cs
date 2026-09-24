using Content.Shared.Language;
namespace Content.Lua.Shared.Language.Components.Translators;

[RegisterComponent]
public sealed partial class HoldsTranslatorComponent : Component
{
    [NonSerialized]
    public HashSet<Entity<HandheldTranslatorComponent>> Translators = new();
}


