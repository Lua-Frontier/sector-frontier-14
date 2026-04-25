using Robust.Shared.Prototypes;

namespace Content.Shared.Research.Prototypes;

[Prototype("rndFaction")]
public sealed partial class RndFactionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("name", required: true)]
    public string Name = string.Empty;
}
