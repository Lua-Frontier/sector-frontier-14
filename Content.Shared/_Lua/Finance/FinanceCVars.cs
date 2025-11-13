/*
 * LuaWorld - This file is licensed under AGPLv3
 * Copyright (c) 2025 LuaWorld Contributors
 * See AGPLv3.txt for details.
 */
using Robust.Shared.Configuration;

namespace Content.Shared._NF.Finance;

/// <summary>
/// CVars for finance system: loans, deposits, dues, holds, and limits.
/// </summary>
[CVarDefs]
public sealed class FinanceCVars
{
    // Credit interest and scheduling
    public static readonly CVarDef<float> CreditShiftInterestPercent =
        CVarDef.Create("bank.credit.shift_interest_percent", 8.0f, CVar.SERVERONLY);

    public static readonly CVarDef<float> CreditShiftInterestMaxPercent =
        CVarDef.Create("bank.credit.shift_interest_max_percent", 13.0f, CVar.SERVERONLY);

    public static readonly CVarDef<int> CreditAutopayPeriodMinutes =
        CVarDef.Create("bank.credit.autopay_period_minutes", 60, CVar.SERVERONLY);

    public static readonly CVarDef<int> CreditAutopayJitterMinutes =
        CVarDef.Create("bank.credit.autopay_jitter_minutes", 15, CVar.SERVERONLY);

    public static readonly CVarDef<float> CreditLateFeePercentPerHour =
        CVarDef.Create("bank.credit.late_fee_percent_per_hour", 1.0f, CVar.SERVERONLY);

    public static readonly CVarDef<float> CreditPenaltyCapPercentPerShift =
        CVarDef.Create("bank.credit.penalty_cap_percent_per_shift", 15.0f, CVar.SERVERONLY);

    public static readonly CVarDef<float> CreditTotalCapPercentPerShift =
        CVarDef.Create("bank.credit.total_cap_percent_per_shift", 20.0f, CVar.SERVERONLY);

    public static readonly CVarDef<int> CreditMaxPrincipalPerLoan =
        CVarDef.Create("bank.credit.max_principal_per_loan", 100_000, CVar.SERVERONLY);

    public static readonly CVarDef<float> CreditDebtCapMultiplier =
        CVarDef.Create("bank.credit.debt_cap_multiplier", 2.0f, CVar.SERVERONLY);

    public static readonly CVarDef<int> CreditMinPlaytimeMinutes =
        CVarDef.Create("bank.credit.min_playtime_minutes", 30, CVar.SERVERONLY);

    public static readonly CVarDef<int> CreditSecondLoanScoreThreshold =
        CVarDef.Create("bank.credit.second_loan_score_threshold", 50, CVar.SERVERONLY);

    public static readonly CVarDef<int> CreditDefaultAfterConsecutiveLateHours =
        CVarDef.Create("bank.credit.default_after_consecutive_late_hours", 4, CVar.SERVERONLY);

    // Baseline for limits
    public static readonly CVarDef<int> BaselineShipPrice =
        CVarDef.Create("bank.credit.baseline.ship_price", 20_000, CVar.SERVERONLY);

    // Deposits
    public static readonly CVarDef<float> DepositShiftInterestPercent =
        CVarDef.Create("bank.credit.deposit.shift_interest_percent", 2.0f, CVar.SERVERONLY);

    public static readonly CVarDef<int> DepositMaxAmount =
        CVarDef.Create("bank.credit.deposit.max_amount", 250_000, CVar.SERVERONLY);

    public static readonly CVarDef<int> DepositCapitalizationMinMinutes =
        CVarDef.Create("bank.credit.deposit.capitalization_min_minutes", 30, CVar.SERVERONLY);

    public static readonly CVarDef<int> DepositCapitalizationMaxMinutes =
        CVarDef.Create("bank.credit.deposit.capitalization_max_minutes", 120, CVar.SERVERONLY);

    public static readonly CVarDef<int> DepositCapitalizationHardStopMinutes =
        CVarDef.Create("bank.credit.deposit.capitalization_hard_stop_minutes", 450, CVar.SERVERONLY);

    public static readonly CVarDef<float> DepositEarlyPenaltyStartPercent =
        CVarDef.Create("bank.credit.deposit.early_penalty_start_percent", 50.0f, CVar.SERVERONLY);

    // Deposit products (short vs long)
    public static readonly CVarDef<int> DepositShortMinMinutes =
        CVarDef.Create("bank.credit.deposit.short_min_minutes", 90, CVar.SERVERONLY); // 1h30m

    public static readonly CVarDef<int> DepositLongMinMinutes =
        CVarDef.Create("bank.credit.deposit.long_min_minutes", 210, CVar.SERVERONLY); // 3h30m

    // Deposit rate models and spreads
    public static readonly CVarDef<float> DepositFixedAprShiftPercent =
        CVarDef.Create("bank.credit.deposit.fixed_apr_shift_percent", 2.0f, CVar.SERVERONLY);

    public static readonly CVarDef<float> DepositBankSpreadPercent =
        CVarDef.Create("bank.credit.deposit.bank_spread_percent", 1.0f, CVar.SERVERONLY); // floating uses credit - spread

    public static readonly CVarDef<float> DepositProgressiveBonusMaxPercent =
        CVarDef.Create("bank.credit.deposit.progressive_bonus_max_percent", 1.0f, CVar.SERVERONLY); // added over base at stop date

    public static readonly CVarDef<bool> DepositEnableDrift =
        CVarDef.Create("bank.credit.deposit.enable_drift", true, CVar.SERVERONLY);

    public static readonly CVarDef<int> DepositDriftMinMinutes =
        CVarDef.Create("bank.credit.deposit.drift_min_minutes", 30, CVar.SERVERONLY);

    public static readonly CVarDef<int> DepositDriftMaxMinutes =
        CVarDef.Create("bank.credit.deposit.drift_max_minutes", 90, CVar.SERVERONLY);

    public static readonly CVarDef<float> DepositShiftInterestMaxPercent =
        CVarDef.Create("bank.credit.deposit.shift_interest_max_percent", 6.0f, CVar.SERVERONLY);

    // Fees and limits
    public static readonly CVarDef<float> DepositOpenFeePercent =
        CVarDef.Create("bank.credit.deposit.open_fee_percent", 0.5f, CVar.SERVERONLY);

    public static readonly CVarDef<float> DepositCloseFeePercent =
        CVarDef.Create("bank.credit.deposit.close_fee_percent", 0.5f, CVar.SERVERONLY);

    public static readonly CVarDef<int> DepositMinAmount =
        CVarDef.Create("bank.credit.deposit.min_amount", 1_000, CVar.SERVERONLY);

    public static readonly CVarDef<int> DepositStepAmount =
        CVarDef.Create("bank.credit.deposit.step_amount", 500, CVar.SERVERONLY);

    public static readonly CVarDef<int> DepositMaxActivePerUser =
        CVarDef.Create("bank.credit.deposit.max_active_per_user", 5, CVar.SERVERONLY);

    public static readonly CVarDef<int> DepositMaxTotalPrincipalPerUser =
        CVarDef.Create("bank.credit.deposit.max_total_principal_per_user", 1_000_000, CVar.SERVERONLY);

    public static readonly CVarDef<int> DepositCloseLockMinutes =
        CVarDef.Create("bank.credit.deposit.close_lock_minutes", 15, CVar.SERVERONLY);

    // Rate limiting
    public static readonly CVarDef<int> DepositOpsMaxPer10Min =
        CVarDef.Create("bank.credit.deposit.ops_max_per_10min", 6, CVar.SERVERONLY);

    public static readonly CVarDef<int> DepositOpsMaxSumPerHour =
        CVarDef.Create("bank.credit.deposit.ops_max_sum_per_hour", 500_000, CVar.SERVERONLY);

    public static readonly CVarDef<int> TransferMaxAmountPerOperation =
        CVarDef.Create("bank.credit.transfer.max_amount_per_op", 2_000_000, CVar.SERVERONLY);

    // Scoring
    public static readonly CVarDef<float> ScorePlaytimeHalfSaturationHours =
        CVarDef.Create("bank.credit.score.playtime_half_sat_hours", 20.0f, CVar.SERVERONLY);

    public static readonly CVarDef<float> ScoreRoleThresholdHours =
        CVarDef.Create("bank.credit.score.role_threshold_hours", 2.0f, CVar.SERVERONLY);

    public static readonly CVarDef<float> ScoreRolePointsPerRole =
        CVarDef.Create("bank.credit.score.role_points_per_role", 4.0f, CVar.SERVERONLY);

    public static readonly CVarDef<float> ScoreRolePointsCap =
        CVarDef.Create("bank.credit.score.role_points_cap", 20.0f, CVar.SERVERONLY);

    public static readonly CVarDef<int> ScoreHistoryWindowHours =
        CVarDef.Create("bank.credit.score.history_window_hours", 60, CVar.SERVERONLY);
}


