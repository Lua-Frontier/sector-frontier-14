// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Content.Shared._NF.Bank;
using Content.Shared._NF.Bank.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Lua.Shared.Bank;

/// <summary>
/// Cross-assembly bank API for Content.Server (which cannot reference Content.Lua.Server).
/// </summary>
public interface IBankSystem : IEntitySystem
{
    bool TryBankWithdraw(EntityUid mobUid, int amount);
    bool TryBankDeposit(EntityUid mobUid, int amount);
    bool TryBankWithdraw(ICommonSession session, int amount, [NotNullWhen(true)] out int? newBalance);
    bool TryBankDeposit(ICommonSession session, int amount, [NotNullWhen(true)] out int? newBalance);
    Task<bool> TryBankWithdrawOffline(NetUserId userId, int amount);
    Task<bool> TryBankDepositOffline(NetUserId userId, int amount);

    bool TryGetBalance(EntityUid ent, out int balance);
    bool TryGetBalance(ICommonSession session, out int balance);
    bool TryGetBalance(NetUserId userId, out int balance);
    bool TryGetBalance(SectorBankAccount account, out int balance, EntityUid? context = null);
    bool HasAccountBank(EntityUid ent);

    bool TrySectorWithdraw(SectorBankAccount account, int amount, LedgerEntryType reason, EntityUid? context = null, SectorBankComponent? bank = null);
    bool TrySectorDeposit(SectorBankAccount account, int amount, LedgerEntryType reason, EntityUid? context = null, SectorBankComponent? bank = null);

    string GetLedgerPrintout();
    bool TryGetOperationHistory(NetUserId userId, out IReadOnlyList<BankAccountOperation> history);
}
