/*
 * LuaWorld - This file is licensed under AGPLv3
 * Copyright (c) 2025 LuaWorld Contributors
 * See AGPLv3.txt for details.
 */
namespace Content.Server._NF.Bank;

/// <summary>
/// Partial extension of BankSystem for YUPI functionality and finance integration
/// </summary>
public sealed partial class BankSystem
{
    /// <summary>
    /// Checks the maximum transfer amount from CVar
    /// </summary>
    private bool CheckTransferLimit(int amount)
    {
        // Temporarily disabled - FinanceCVars not available
        // var cvarMax = IoCManager.Resolve<IConfigurationManager>().GetCVar(FinanceCVars.TransferMaxAmountPerOperation);
        // return amount <= cvarMax;
        return true; // Allow all transfers until configured
    }
}
