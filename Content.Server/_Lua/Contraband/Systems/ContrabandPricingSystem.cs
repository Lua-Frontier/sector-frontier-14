// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Content.Shared.Contraband;
using Content.Shared.Store;

namespace Content.Server._Lua.Contraband.Systems;

public sealed class ContrabandPricingSystem : EntitySystem
{
    public bool TryGetItemPrice(EntityUid item, ProtoId<CurrencyPrototype> currency, out int price)
    {
        price = GetItemPrice(item, currency);
        return price != 0;
    }

    public int GetItemPrice(EntityUid item, ProtoId<CurrencyPrototype> currency)
    {
        var price = 0;

        if (TryComp<ContrabandComponent>(item, out var contrabandComp)
            && contrabandComp.TurnInValues.TryGetValue(currency, out var contrabandValue)
            && contrabandValue != 0)
        {
            price += contrabandValue;
        }

        if (!TryComp<ContainerManagerComponent>(item, out var containerComp))
        {
            return price;
        }

        foreach (var container in containerComp.Containers.Values)
        {
            foreach (var nestedItem in container.ContainedEntities)
            {
                price += GetItemPrice(nestedItem, currency);
            }
        }

        return price;
    }

    public bool TryGetItemPrice(EntityUid item, ProtoId<CurrencyPrototype> currency, out int price, ref HashSet<EntityUid> nestedItems)
    {
        price = GetItemPrice(item, currency, ref nestedItems);
        return price != 0;
    }

    public int GetItemPrice(EntityUid item, ProtoId<CurrencyPrototype> currency, ref HashSet<EntityUid> nestedItems)
    {
        var price = 0;

        if (TryComp<ContrabandComponent>(item, out var contrabandComp)
            && contrabandComp.TurnInValues.TryGetValue(currency, out var contrabandValue)
            && contrabandValue != 0)
        {
            nestedItems.Add(item);
            price += contrabandValue;
        }

        if (!TryComp<ContainerManagerComponent>(item, out var containerComp))
        {
            return price;
        }

        foreach (var container in containerComp.Containers.Values)
        {
            foreach (var nestedItem in container.ContainedEntities)
            {
                if (TryGetItemPrice(nestedItem, currency, out var itemPrice, ref nestedItems))
                {
                    price += itemPrice;
                }
            }
        }

        return price;
    }

    public bool TryGetItemPrice(EntityUid item, ProtoId<CurrencyPrototype> currency, Predicate<EntityUid> predicate, out int price, ref HashSet<EntityUid> nestedItems)
    {
        price = GetItemPrice(item, currency, predicate, ref nestedItems);
        return price != 0;
    }

    public int GetItemPrice(EntityUid item, ProtoId<CurrencyPrototype> currency, Predicate<EntityUid> predicate, ref HashSet<EntityUid> nestedItems)
    {
        var price = 0;

        if (!predicate(item))
        {
            return price;
        }

        if (TryComp<ContrabandComponent>(item, out var contrabandComp)
            && contrabandComp.TurnInValues.TryGetValue(currency, out var contrabandValue)
            && contrabandValue != 0)
        {
            nestedItems.Add(item);
            price += contrabandValue;
        }

        if (!TryComp<ContainerManagerComponent>(item, out var containerComp))
        {
            return price;
        }

        foreach (var container in containerComp.Containers.Values)
        {
            foreach (var nestedItem in container.ContainedEntities)
            {
                if (TryGetItemPrice(nestedItem, currency, predicate, out var itemPrice, ref nestedItems))
                {
                    price += itemPrice;
                }
            }
        }

        return price;
    }
}
