/*
 * LuaWorld - This file is licensed under AGPLv3
 * Copyright (c) 2025 LuaWorld Contributors
 * See AGPLv3.txt for details.
 */

using Content.Shared.Containers.ItemSlots;

namespace Content.Server._NF.Finance.Components;

[RegisterComponent, ComponentProtoName("FinanceDepositConsole")]
public sealed partial class FinanceDepositConsoleComponent : Component
{
    public static string TargetIdCardSlotId = "FinanceDepositConsole-targetId";

    [DataField]
    public ItemSlot TargetIdSlot = new();
}


