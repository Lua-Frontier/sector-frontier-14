using System.Collections.Generic;
using Robust.Shared.GameObjects;

namespace Content.Lua.Shared.DynamicMarket;

public interface IDynamicMarketDbSystem : IEntitySystem
{
    double GetCurrentMultiplier(string prototypeId);
    double GetProjectedMultiplierAfterSale(string prototypeId, int units);
    void ApplySale(IReadOnlyCollection<(string prototypeId, int units, double baseUnitPrice)> sold);
    void ApplyPurchase(IReadOnlyCollection<(string prototypeId, int units, double baseUnitPrice)> bought);
}
