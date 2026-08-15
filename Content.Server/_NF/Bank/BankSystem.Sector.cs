using System.Runtime.InteropServices;
using Content.Server._NF.SectorServices;
using Content.Shared._NF.Bank.BUI;
using Content.Shared._NF.Bank;
using Content.Shared._NF.Bank.Components;
using JetBrains.Annotations;

namespace Content.Server._NF.Bank;

public sealed partial class BankSystem : SharedBankSystem
{
    [Dependency] private readonly SectorServiceSystem _sectorService = default!;

    private const float AccountIncreaseInterval = 10.0f;

    private void OnSectorInit(EntityUid entity, SectorBankComponent component, ComponentInit args)
    {
        foreach (var account in component.Accounts)
            AddLedgerEntry(account.Key, LedgerEntryType.TickingIncome, account.Value.Balance, component);
    }

    private bool TryResolveSectorBank(EntityUid? context, SectorBankComponent? bank, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out SectorBankComponent? resolved)
    {
        if (bank != null)
        {
            resolved = bank;
            return true;
        }

        if (context != null && _sectorService.TryGetServiceEntity(context.Value, out var localService) && TryComp(localService, out resolved))
            return true;

        if (TryComp(_sectorService.GetServiceEntity(), out resolved))
            return true;

        resolved = null;
        return false;
    }

    [PublicAPI]
    public bool TrySectorWithdraw(SectorBankAccount account, int amount, LedgerEntryType reason, EntityUid? context = null, SectorBankComponent? bank = null)
    {
        if (amount <= 0)
        {
            _log.Info($"TryBankWithdraw: {amount} is invalid");
            return false;
        }

        if (!TryResolveSectorBank(context, bank, out bank))
        {
            _log.Info($"TryBankWithdraw: no bank component");
            return false;
        }

        if (!bank.Accounts.ContainsKey(account))
        {
            _log.Info($"TryBankWithdraw: invalid account");
            return false;
        }

        var bankAccount = CollectionsMarshal.GetValueRefOrNullRef(bank.Accounts, account);
        if (bankAccount.Balance < amount)
        {
            _log.Info($"TryBankWithdraw: account has less money {bankAccount.Balance} than requested {amount}");
            return false;
        }

        bankAccount.Balance -= amount;
        AddLedgerEntry(account, reason, amount, bank);
        return true;
    }

    [PublicAPI]
    public bool TrySectorDeposit(SectorBankAccount account, int amount, LedgerEntryType reason, EntityUid? context = null, SectorBankComponent? bank = null)
    {
        if (amount <= 0)
        {
            _log.Info($"TryBankDeposit: {amount} is invalid");
            return false;
        }

        if (!TryResolveSectorBank(context, bank, out bank))
        {
            _log.Info($"TryBankDeposit: no bank component");
            return false;
        }

        if (!bank.Accounts.ContainsKey(account))
        {
            _log.Info($"TryBankDeposit: invalid account");
            return false;
        }

        var bankAccount = CollectionsMarshal.GetValueRefOrNullRef(bank.Accounts, account);
        bankAccount.Balance += amount;
        AddLedgerEntry(account, reason, amount, bank);
        return true;
    }

    [PublicAPI]
    public bool TryGetBalance(SectorBankAccount account, out int balance, EntityUid? context = null)
    {
        if (!TryResolveSectorBank(context, null, out var bank))
        {
            _log.Info($"TryGetBalance: no bank component");
            balance = 0;
            return false;
        }

        if (!bank.Accounts.ContainsKey(account))
        {
            _log.Info($"TryGetBalance: invalid account");
            balance = 0;
            return false;
        }

        balance = bank.Accounts[account].Balance;
        return true;
    }

    private void UpdateSectorBanks(float frameTime)
    {
        foreach (var service in _sectorService.GetServiceEntities())
        {
            if (!TryComp(service, out SectorBankComponent? bank))
                continue;

            bank.SecondsSinceLastIncrease += frameTime;

            float secondsToCredit = 0;
            while (bank.SecondsSinceLastIncrease > AccountIncreaseInterval)
            {
                bank.SecondsSinceLastIncrease -= AccountIncreaseInterval;
                secondsToCredit += AccountIncreaseInterval;
            }

            var seconds = (int)secondsToCredit;
            if (seconds <= 0)
                continue;

            foreach (var (accountId, accountInfo) in bank.Accounts)
                TrySectorDeposit(accountId, seconds * accountInfo.IncreasePerSecond, LedgerEntryType.TickingIncome, bank: bank);
        }
    }
}
