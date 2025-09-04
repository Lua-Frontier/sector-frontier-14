/*
 * LuaWorld - This file is licensed under AGPLv3
 * Copyright (c) 2025 LuaWorld Contributors
 * See AGPLv3.txt for details.
 */
using System;
using Content.Shared.UserInterface;
using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Finance.Events;

// Rating console messages

[Serializable, NetSerializable]
public sealed class FinanceRatingQueryMessage : BoundUserInterfaceMessage
{
    public readonly string QueryName;
    public FinanceRatingQueryMessage(string queryName)
    {
        QueryName = queryName;
    }
}

// Issuance console messages

[Serializable, NetSerializable]
public sealed class FinanceIssueLoanRequestMessage : BoundUserInterfaceMessage
{
    public readonly int Amount;
    public FinanceIssueLoanRequestMessage(int amount)
    {
        Amount = amount;
    }
}

[Serializable, NetSerializable]
public sealed class FinanceIssueLoanResponseState : BoundUserInterfaceState
{
    public readonly bool Success;
    public readonly string Message;
    public FinanceIssueLoanResponseState(bool success, string message)
    {
        Success = success;
        Message = message;
    }
}

// Common status request/response

[Serializable, NetSerializable]
public sealed class FinanceStatusQueryMessage : BoundUserInterfaceMessage
{
}

// Deposit console messages

[Serializable, NetSerializable]
public enum DepositRateModel
{
    FixedApr,
    FloatingApr,
    ProgressiveApr
}

[Serializable, NetSerializable]
public enum DepositTermType
{
    Short,
    Long
}

[Serializable, NetSerializable]
public sealed class FinanceOpenDepositRequestMessage : BoundUserInterfaceMessage
{
    public readonly int Amount;
    public readonly DepositTermType Term;
    public readonly DepositRateModel Rate;
    public FinanceOpenDepositRequestMessage(int amount, DepositTermType term, DepositRateModel rate)
    {
        Amount = amount;
        Term = term;
        Rate = rate;
    }
}

[Serializable, NetSerializable]
public sealed class FinanceCloseDepositRequestMessage : BoundUserInterfaceMessage
{
    public readonly int DepositId;
    public FinanceCloseDepositRequestMessage(int depositId)
    {
        DepositId = depositId;
    }
}

[Serializable, NetSerializable]
public sealed class FinanceTopUpDepositRequestMessage : BoundUserInterfaceMessage
{
    public readonly int DepositId;
    public readonly int Amount;
    public FinanceTopUpDepositRequestMessage(int depositId, int amount)
    {
        DepositId = depositId;
        Amount = amount;
    }
}

[Serializable, NetSerializable]
public sealed class FinancePartialWithdrawDepositRequestMessage : BoundUserInterfaceMessage
{
    public readonly int DepositId;
    public readonly int Amount;
    public FinancePartialWithdrawDepositRequestMessage(int depositId, int amount)
    {
        DepositId = depositId;
        Amount = amount;
    }
}

[Serializable, NetSerializable]
public sealed class FinanceStatusState : BoundUserInterfaceState
{
    public readonly int Balance;
    public readonly int Available;
    public readonly int Due;
    public readonly int Hold;
    public readonly TimeSpan? NextChargeEta;
    public readonly int MinAmount;
    public readonly int MaxAmount;
    public readonly int StepAmount;
    public FinanceStatusState(int balance, int available, int due, int hold, TimeSpan? nextChargeEta, int minAmount, int maxAmount, int stepAmount)
    {
        Balance = balance;
        Available = available;
        Due = due;
        Hold = hold;
        NextChargeEta = nextChargeEta;
        MinAmount = minAmount;
        MaxAmount = maxAmount;
        StepAmount = stepAmount;
    }
}

[Serializable, NetSerializable]
public sealed class FinanceDepositListQueryMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class FinanceDepositListState : BoundUserInterfaceState
{
    public readonly FinanceDepositRow[] Rows;
    public FinanceDepositListState(FinanceDepositRow[] rows)
    {
        Rows = rows;
    }
}

[Serializable, NetSerializable]
public sealed class FinanceDepositRow
{
    public readonly int Id;
    public readonly int Principal;
    public readonly int Accrued;
    public readonly DepositRateModel RateModel;
    public readonly float AprPercent;
    public readonly int NextCapSeconds;
    public readonly int StopAtSeconds;
    public readonly int EarlyPenaltyPreview;

    public FinanceDepositRow(int id, int principal, int accrued, DepositRateModel rateModel, float aprPercent, int nextCapSeconds, int stopAtSeconds, int earlyPenaltyPreview)
    {
        Id = id;
        Principal = principal;
        Accrued = accrued;
        RateModel = rateModel;
        AprPercent = aprPercent;
        NextCapSeconds = nextCapSeconds;
        StopAtSeconds = stopAtSeconds;
        EarlyPenaltyPreview = earlyPenaltyPreview;
    }
}

[Serializable, NetSerializable]
public sealed class FinanceRatingState : BoundUserInterfaceState
{
    public readonly string Query;
    public readonly int Score;
    public readonly int MaxLoan;
    public readonly int ActiveLoans;
    public readonly string? TargetName;
    public readonly int? NextChargeSeconds;
    public FinanceRatingState(string query, int score, int maxLoan, int activeLoans, string? targetName = null, int? nextChargeSeconds = null)
    {
        Query = query;
        Score = score;
        MaxLoan = maxLoan;
        ActiveLoans = activeLoans;
        TargetName = targetName;
        NextChargeSeconds = nextChargeSeconds;
    }
}

// Loans console

[Serializable, NetSerializable]
public sealed class FinanceLoansQueryMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class FinanceLoansState : BoundUserInterfaceState
{
    public readonly FinanceLoanRow[] Rows;
    public FinanceLoansState(FinanceLoanRow[] rows)
    {
        Rows = rows;
    }
}

[Serializable, NetSerializable]
public sealed class FinanceLoansConfigState : BoundUserInterfaceState
{
    public readonly bool ShowLoans;
    public readonly bool ShowDeposits;
    public readonly bool AllowForceClose;
    public FinanceLoansConfigState(bool showLoans, bool showDeposits, bool allowForceClose)
    {
        ShowLoans = showLoans;
        ShowDeposits = showDeposits;
        AllowForceClose = allowForceClose;
    }
}

[Serializable, NetSerializable]
public sealed class FinanceLoanRow
{
    public readonly string Name;
    public readonly int Principal;
    public readonly int SecondsUntilCharge;
    public readonly string YupiCode;

    public FinanceLoanRow(string name, int principal, int secondsUntilCharge, string yupiCode)
    {
        Name = name;
        Principal = principal;
        SecondsUntilCharge = secondsUntilCharge;
        YupiCode = yupiCode;
    }
}

// Deposits overview console (like loans overview)

[Serializable, NetSerializable]
public sealed class FinanceDepositsQueryMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class FinanceDepositsState : BoundUserInterfaceState
{
    public readonly FinanceDepositOverviewRow[] Rows;
    public FinanceDepositsState(FinanceDepositOverviewRow[] rows)
    {
        Rows = rows;
    }
}

[Serializable, NetSerializable]
public sealed class FinanceDepositOverviewRow
{
    public readonly string Name;
    public readonly string YupiCode;
    public readonly int DepositId;
    public readonly int Principal;
    public readonly int Accrued;
    public readonly int NextCapSeconds;
    public readonly int StopAtSeconds;
    public readonly DepositRateModel RateModel;

    public FinanceDepositOverviewRow(string name, string yupiCode, int depositId, int principal, int accrued, int nextCapSeconds, int stopAtSeconds, DepositRateModel rateModel)
    {
        Name = name;
        YupiCode = yupiCode;
        DepositId = depositId;
        Principal = principal;
        Accrued = accrued;
        NextCapSeconds = nextCapSeconds;
        StopAtSeconds = stopAtSeconds;
        RateModel = rateModel;
    }
}


