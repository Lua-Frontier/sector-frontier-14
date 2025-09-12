/*
 * LuaWorld - This file is licensed under AGPLv3
 * Copyright (c) 2025 LuaWorld Contributors
 * See AGPLv3.txt for details.
 */
using Content.Server._NF.Finance.Components;
using Content.Shared.Containers.ItemSlots;

namespace Content.Server._Lua.Finance;

public sealed class FinanceConsoleSlotsSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlotsSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FinanceDepositConsoleComponent, ComponentInit>(OnDepositConsoleInit);
        SubscribeLocalEvent<FinanceDepositConsoleComponent, ComponentRemove>(OnDepositConsoleRemove);

        SubscribeLocalEvent<FinanceIssuanceConsoleComponent, ComponentInit>(OnIssuanceConsoleInit);
        SubscribeLocalEvent<FinanceIssuanceConsoleComponent, ComponentRemove>(OnIssuanceConsoleRemove);

        SubscribeLocalEvent<FinanceRatingConsoleComponent, ComponentInit>(OnRatingConsoleInit);
        SubscribeLocalEvent<FinanceRatingConsoleComponent, ComponentRemove>(OnRatingConsoleRemove);
    }

    private void OnDepositConsoleInit(EntityUid uid, FinanceDepositConsoleComponent component, ComponentInit args)
    {
        _itemSlotsSystem.AddItemSlot(uid, FinanceDepositConsoleComponent.TargetIdCardSlotId, component.TargetIdSlot);
    }

    private void OnDepositConsoleRemove(EntityUid uid, FinanceDepositConsoleComponent component, ComponentRemove args)
    {
        _itemSlotsSystem.RemoveItemSlot(uid, component.TargetIdSlot);
    }

    private void OnIssuanceConsoleInit(EntityUid uid, FinanceIssuanceConsoleComponent component, ComponentInit args)
    {
        _itemSlotsSystem.AddItemSlot(uid, FinanceIssuanceConsoleComponent.PrivilegedIdCardSlotId, component.PrivilegedIdSlot);
    }

    private void OnIssuanceConsoleRemove(EntityUid uid, FinanceIssuanceConsoleComponent component, ComponentRemove args)
    {
        _itemSlotsSystem.RemoveItemSlot(uid, component.PrivilegedIdSlot);
    }

    private void OnRatingConsoleInit(EntityUid uid, FinanceRatingConsoleComponent component, ComponentInit args)
    {
        _itemSlotsSystem.AddItemSlot(uid, FinanceRatingConsoleComponent.TargetIdCardSlotId, component.TargetIdSlot);
    }

    private void OnRatingConsoleRemove(EntityUid uid, FinanceRatingConsoleComponent component, ComponentRemove args)
    {
        _itemSlotsSystem.RemoveItemSlot(uid, component.TargetIdSlot);
    }
}


