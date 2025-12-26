/*
 * LuaWorld - This file is licensed under AGPLv3
 * Copyright (c) 2025 LuaWorld Contributors
 * See AGPLv3.txt for details.
 */
using Content.Shared.Lua.CLVar;

namespace Content.Server._NF.Bank;

public sealed partial class BankSystem
{
    private bool CheckTransferLimit(int amount)
    {
        var max = _cfg.GetCVar(CLVars.TransferMaxAmountPerOperation);
        return max <= 0 || amount <= max;
    }
}
