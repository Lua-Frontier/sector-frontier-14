using Robust.Shared.Prototypes;

namespace Content.Shared._Lua.Market.Prototypes;

/// <summary>
/// Prototype to configure market domain pricing and per-category dynamic parameters.
/// One prototype per domain (Default, Syndicate, BlackMarket, etc.).
/// </summary>
[Prototype("marketDomainConfig")]
public sealed partial class MarketDomainConfigPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Base price multiplier applied for this domain, on top of console-specific MarketModifier.
    /// Default 1.0.
    /// </summary>
    [DataField("baseMultiplier")]
    public float BaseMultiplier { get; private set; } = 1.0f;

    /// <summary>
    /// Parameters per category.
    /// Keys must match values from server-side category enum mapping.
    /// </summary>
    [DataField("categories")]
    public Dictionary<string, CategoryParamsPrototype> Categories { get; private set; } = new();

    // Online-based scaling configuration
    [DataField("onlineMin")] public int OnlineMin { get; private set; } = 0;
    [DataField("onlineMax")] public int OnlineMax { get; private set; } = 110;
    [DataField("onlineScaleMin")] public double OnlineScaleMin { get; private set; } = 0.9;
    [DataField("onlineScaleMax")] public double OnlineScaleMax { get; private set; } = 1.1;

    // Default dynamic parameters used as fallback (e.g., for Unknown category)
    [DataField("defaultDecayPerStack")] public double DefaultDecayPerStack { get; private set; } = 0.003;
    [DataField("defaultBulkDecayPerStack")] public double DefaultBulkDecayPerStack { get; private set; } = 0.0007;
    [DataField("defaultRestorePerMinute")] public double DefaultRestorePerMinute { get; private set; } = 0.01;
    [DataField("defaultMinAfterTaxBaseFraction")] public double DefaultMinAfterTaxBaseFraction { get; private set; } = 0.25;

    [DataDefinition]
    public sealed partial class CategoryParamsPrototype
    {
        [DataField("decayPerStack")]
        public double DecayPerStack = 0.01;

        [DataField("bulkDecayPerStack")]
        public double BulkDecayPerStack = 0.002;

        [DataField("restorePerMinute")]
        public double RestorePerMinute = 0.01;

        [DataField("minAfterTaxBaseFraction")]
        public double MinAfterTaxBaseFraction = 0.25;
    }
}


