using Robust.Shared.Prototypes;

[Prototype]
public sealed class ShuttleCategoryLimitPrototype : IPrototype
{
    [IdDataField] public string ID { get; set; } = default!;
    [DataField("categoryId")] public string CategoryId = string.Empty;
    [DataField("maxInCategory")] public int MaxInCategory = 1;
}
