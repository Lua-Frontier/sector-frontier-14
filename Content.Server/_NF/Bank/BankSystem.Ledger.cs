using System.Text;
using Content.Shared._NF.Bank;
using Content.Shared._NF.Bank.Components;

namespace Content.Server._NF.Bank;

public sealed partial class BankSystem : SharedBankSystem
{
    public void CleanupLedger()
    {
        foreach (var service in _sectorService.GetServiceEntities())
        {
            if (!TryComp(service, out SectorBankComponent? ledger))
                continue;
            ledger.AccountLedgerEntries.Clear();
        }
    }

    public void AddLedgerEntry(SectorBankAccount account, LedgerEntryType entryType, int amount, SectorBankComponent? ledger = null)
    {
        if (amount <= 0)
            return;

        if (ledger == null && !TryResolveSectorBank(null, null, out ledger))
            return;

        var tuple = (account, entryType);
        if (ledger.AccountLedgerEntries.ContainsKey(tuple))
            ledger.AccountLedgerEntries[tuple] += amount;
        else
            ledger.AccountLedgerEntries[tuple] = amount;
        RaiseLocalEvent(new SectorLedgerUpdatedEvent());
    }

    sealed class AccountInfo
    {
        public int TotalIncome;
        public int TotalExpenses;
        public List<(LedgerEntryType Type, int Value)> Income = new();
        public List<(LedgerEntryType Type, int Value)> Expenses = new();
    }

    public string GetLedgerPrintout()
    {
        var builder = new StringBuilder();
        var any = false;

        foreach (var service in _sectorService.GetServiceEntities())
        {
            if (!TryComp(service, out SectorBankComponent? ledger))
                continue;

            any = true;
            AppendLedger(builder, ledger);
            builder.AppendLine();
        }

        return any ? builder.ToString() : string.Empty;
    }

    private void AppendLedger(StringBuilder builder, SectorBankComponent ledger)
    {
        var accountDict = new Dictionary<SectorBankAccount, AccountInfo>();
        foreach (var value in Enum.GetValues<SectorBankAccount>())
        {
            if (value == SectorBankAccount.Invalid)
                continue;
            accountDict[value] = new AccountInfo();
        }

        foreach (var (ledgerEntry, value) in ledger.AccountLedgerEntries)
        {
            if (!accountDict.ContainsKey(ledgerEntry.Account))
                continue;
            if (ledgerEntry.Type >= LedgerEntryType.FirstExpense)
            {
                accountDict[ledgerEntry.Account].Expenses.Add((ledgerEntry.Type, value));
                accountDict[ledgerEntry.Account].TotalExpenses += value;
            }
            else
            {
                accountDict[ledgerEntry.Account].Income.Add((ledgerEntry.Type, value));
                accountDict[ledgerEntry.Account].TotalIncome += value;
            }
        }

        foreach (var (account, accountInfo) in accountDict)
        {
            builder.AppendLine(Loc.GetString("ledger-printout-account", ("account", Loc.GetString($"ledger-tab-{account}"))));
            builder.AppendLine(Loc.GetString("ledger-printout-income-header"));
            foreach (var income in accountInfo.Income)
            {
                builder.AppendLine(
                    Loc.GetString("ledger-printout-line-item",
                        ("entryType", Loc.GetString($"ledger-entry-type-{income.Type}")),
                        ("amount", BankSystemExtensions.ToSpesoString(income.Value))
                    ));
            }
            builder.AppendLine(
                Loc.GetString("ledger-printout-total-income",
                    ("amount", BankSystemExtensions.ToSpesoString(accountInfo.TotalIncome))
                ));
            builder.AppendLine();
            builder.AppendLine(Loc.GetString("ledger-printout-expense-header"));
            foreach (var expense in accountInfo.Expenses)
            {
                builder.AppendLine(
                    Loc.GetString("ledger-printout-line-item",
                        ("entryType", Loc.GetString($"ledger-entry-type-{expense.Type}")),
                        ("amount", BankSystemExtensions.ToSpesoString(expense.Value))
                    ));
            }
            builder.AppendLine(
                Loc.GetString("ledger-printout-total-expenses",
                    ("amount", BankSystemExtensions.ToSpesoString(accountInfo.TotalExpenses))
                ));
            builder.AppendLine(
                Loc.GetString("ledger-printout-balance",
                    ("amount", BankSystemExtensions.ToSpesoString(accountInfo.TotalIncome - accountInfo.TotalExpenses))
                ));
            builder.AppendLine();
        }
    }
}

public sealed class SectorLedgerUpdatedEvent : EntityEventArgs;
