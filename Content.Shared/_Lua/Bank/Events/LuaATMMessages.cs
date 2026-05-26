// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Bank.Events;

[Serializable, NetSerializable]
public sealed class LuaATMPersonalInfoMessage(bool enabled, int balance, string yupiCode, List<BankAccountOperation> history) : BoundUserInterfaceMessage
{
    public bool Enabled = enabled;
    public int Balance = balance;
    public string YUPICode = yupiCode;
    public List<BankAccountOperation> History = history;
}
