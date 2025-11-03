using Content.Shared.Backmen.Disease;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Disease.Components;

[RegisterComponent]
public sealed partial class InfectOnMeleeComponent : Component
{
    [DataField("disease", required: true, serverOnly: true)]
    public ProtoId<DiseasePrototype> Disease = default!;

    [DataField("infectChance", serverOnly: true)]
    public float InfectChance = 0.05f;
}

