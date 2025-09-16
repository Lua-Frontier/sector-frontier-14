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
    public readonly int Outstanding;
    public readonly int Due;

    public YupiTransferUiState(string ownCode, int balance, int outstanding = 0, int due = 0)
    {
        OwnCode = ownCode;
        Balance = balance;
        Outstanding = outstanding;
        Due = due;
    }
}


