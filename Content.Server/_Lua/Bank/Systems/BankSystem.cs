// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Timing;
using Content.Shared._Lua.Bank;
using Content.Shared._NF.Bank.Components;

namespace Content.Server._NF.Bank;

public sealed partial class BankSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private void AddOperationRecord(BankAccountComponent comp, BankAccountOperationType type, int value)
    {
        comp.OperationHistory.Add(new(type, value, _timing.CurTime));

        if (comp.OperationHistory.Count > 50)
        {
            comp.OperationHistory.RemoveRange(0, 30);
        }
    }
}
