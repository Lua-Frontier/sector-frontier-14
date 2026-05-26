// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Bank;

[Serializable, NetSerializable]
public enum BankAccountOperationType : byte
{
    Deposit,
    Withdraw
}
