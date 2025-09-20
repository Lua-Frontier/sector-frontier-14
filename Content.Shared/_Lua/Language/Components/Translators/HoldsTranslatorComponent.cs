namespace Content.Shared._Lua.Language.Components.Translators;

[RegisterComponent]
public sealed partial class HoldsTranslatorComponent : Component
{
    [NonSerialized]
    public HashSet<Entity<HandheldTranslatorComponent>> Translators = new();
}


