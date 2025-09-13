// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.
using Robust.Shared.Prototypes;

namespace Content.Shared._Lua.Market.Prototypes;

[Prototype("marketDomainConfig")]
public sealed partial class MarketDomainConfigPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public float BaseMultiplier { get; private set; } = 1.0f;

    [DataField]
    public Dictionary<string, CategoryParamsPrototype> Categories { get; private set; } = new();

    [DataField]
    public int OnlineMin { get; private set; } = 0;

    [DataField]
    public int OnlineMax { get; private set; } = 110;

    [DataField]
    public double OnlineScaleMin { get; private set; } = 0.9;

    [DataField]
    public double OnlineScaleMax { get; private set; } = 1.1;

    [DataField]
    public double DefaultDecayPerStack { get; private set; } = 0.003;

    [DataField]
    public double DefaultBulkDecayPerStack { get; private set; } = 0.0007;

    [DataField]
    public double DefaultRestorePerMinute { get; private set; } = 0.01;

    [DataField]
    public double DefaultMinAfterTaxBaseFraction { get; private set; } = 0.25;

    [DataDefinition]
    public sealed partial class CategoryParamsPrototype
    {
        [DataField]
        public double DecayPerStack = 0.01;

        [DataField]
        public double BulkDecayPerStack = 0.002;

        [DataField]
        public double RestorePerMinute = 0.01;

        [DataField]
        public double MinAfterTaxBaseFraction = 0.25;
    }
}


