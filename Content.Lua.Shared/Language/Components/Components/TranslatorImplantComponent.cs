using Content.Lua.Shared.Language.Components.Translators;
using Content.Shared.Language;

namespace Content.Lua.Shared.Language.Components;

[RegisterComponent]
public sealed partial class TranslatorImplantComponent : BaseTranslatorComponent
{
    public bool SpokenRequirementSatisfied = false;
    public bool UnderstoodRequirementSatisfied = false;
}
