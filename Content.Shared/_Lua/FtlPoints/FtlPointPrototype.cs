using Robust.Shared.Prototypes;

namespace Content.Shared._Lua.FtlPoints;

[Prototype("ftlPoint")]
public sealed class FtlPointPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = default!;

    [DataField]
    public float Probability = 1.0f;

    [DataField]
    public LocId Tag = "";

    [DataField]
    public string PointType = "Star";

    [DataField]
    public string? MapPrototype;

    [DataField]
    public bool GenerateSector = false;
}
