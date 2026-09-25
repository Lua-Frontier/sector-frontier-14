using Content.Shared.Tools;
using Robust.Shared.Prototypes;

namespace Content.Shared._NF.Digging.Components;

[RegisterComponent]
public sealed partial class EarthDiggingComponent : Component
{
    [ViewVariables, DataField]
    public bool ToolComponentNeeded = true;

    public ProtoId<ToolQualityPrototype> QualityNeeded = "Digging";

    [ViewVariables, DataField]
    public float Delay = 2f;

}
