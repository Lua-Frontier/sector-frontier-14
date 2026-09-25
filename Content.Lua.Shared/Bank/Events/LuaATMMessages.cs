// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;

namespace Content.Lua.Shared.Bank.Events;

[Serializable, NetSerializable]
public sealed class LuaATMPersonalInfoMessage(bool enabled, int balance, string galBankCode, List<BankAccountOperation> history) : BoundUserInterfaceMessage
{
    public bool Enabled = enabled;
    public int Balance = balance;
    public string GalBankCode = galBankCode;
    public List<BankAccountOperation> History = history;
}
