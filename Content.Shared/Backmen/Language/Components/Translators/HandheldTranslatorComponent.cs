namespace Content.Shared.Backmen.Language.Components.Translators;

[RegisterComponent]
public sealed partial class HandheldTranslatorComponent : Translators.BaseTranslatorComponent
{
    [DataField]
    public bool ToggleOnInteract = true;

    [DataField]
    public bool SetLanguageOnInteract = true;
}


