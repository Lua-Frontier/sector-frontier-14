using Robust.Shared.Prototypes;

namespace Content.Shared.Prototypes;

[Prototype("optionalEntityPrototype")]
public sealed partial class OptionalEntityPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;
}

