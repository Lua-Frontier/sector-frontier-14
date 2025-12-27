/*
 * LuaWorld - This file is licensed under AGPLv3
 * Copyright (c) 2025 LuaWorld Contributors
 * See AGPLv3.txt for details.
 */
using Robust.Shared.Serialization;

namespace Content.Shared._NF.Bank.BUI;

[Serializable, NetSerializable]
public sealed class YupiTransferUiState : BoundUserInterfaceState
{
    public readonly string OwnCode;
    public readonly int Balance;

    public YupiTransferUiState(string ownCode, int balance)
    {
        OwnCode = ownCode;
        Balance = balance;
    }
}


