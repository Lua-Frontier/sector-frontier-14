using System;
using System.Collections.Generic;
using Content.Shared.Store;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Lua.Shared.Contraband;

public interface IContrabandPricingSystem : IEntitySystem
{
    bool TryGetItemPrice(EntityUid item, ProtoId<CurrencyPrototype> currency, out int price);
    bool TryGetItemPrice(EntityUid item, ProtoId<CurrencyPrototype> currency, Predicate<EntityUid> predicate, out int price, ref HashSet<EntityUid> nestedItems);
}
