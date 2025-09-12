/*
 * LuaWorld - This file is licensed under AGPLv3
 * Copyright (c) 2025 LuaWorld Contributors
 * See AGPLv3.txt for details.
 */
using Content.Shared.Containers.ItemSlots;

namespace Content.Server._NF.Finance.Components;

[RegisterComponent, ComponentProtoName("FinanceIssuanceConsole")]
public sealed partial class FinanceIssuanceConsoleComponent : Component
{
    public static string PrivilegedIdCardSlotId = "FinanceIssuanceConsole-privilegedId";

    [DataField]
    public ItemSlot PrivilegedIdSlot = new();
}


