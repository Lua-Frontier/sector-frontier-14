using Content.Shared._Lua.Language.Components.Translators;

namespace Content.Shared._Lua.Language.Components;

[RegisterComponent]
public sealed partial class TranslatorImplantComponent : BaseTranslatorComponent
{
    public bool SpokenRequirementSatisfied = false;
    public bool UnderstoodRequirementSatisfied = false;
}
