// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;

namespace Content.Lua.Shared.GalBank.BUI;

[Serializable, NetSerializable]
public sealed class GalBankTransferUiState : BoundUserInterfaceState
{
    public readonly string OwnCode;
    public readonly int Balance;

    public GalBankTransferUiState(string ownCode, int balance)
    {
        OwnCode = ownCode;
        Balance = balance;
    }
}
