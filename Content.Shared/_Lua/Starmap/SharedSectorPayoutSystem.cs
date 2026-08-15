// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Lua.Starmap.Components;
using Content.Shared.Containers.ItemSlots;

namespace Content.Shared._Lua.Starmap;

public abstract class SharedSectorPayoutSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FactionPayoutCollectorComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<FactionPayoutCollectorComponent, ComponentRemove>(OnRemove);
    }

    private void OnInit(EntityUid uid, FactionPayoutCollectorComponent component, ComponentInit args)
    {
        _itemSlots.AddItemSlot(uid, FactionPayoutCollectorComponent.CashSlotId, component.CashSlot);
    }

    private void OnRemove(EntityUid uid, FactionPayoutCollectorComponent component, ComponentRemove args)
    {
        _itemSlots.RemoveItemSlot(uid, component.CashSlot);
    }
}
