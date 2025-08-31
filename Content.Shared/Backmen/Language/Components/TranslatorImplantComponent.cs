using Content.Shared.Backmen.Language.Components.Translators;

namespace Content.Shared.Backmen.Language.Components;

[RegisterComponent]
public sealed partial class TranslatorImplantComponent : BaseTranslatorComponent
{
    public bool SpokenRequirementSatisfied = false;
    public bool UnderstoodRequirementSatisfied = false;
}
