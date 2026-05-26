// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Bank.UI;

[Serializable, NetSerializable]
public sealed class LuaATMMenuInterfaceState(bool corrupted, bool withdrawOnly, int deposit) : BoundUserInterfaceState
{
    public bool Corrupted = corrupted;
    public bool WithdrawOnly = withdrawOnly;
    public int Deposit = deposit;
}
