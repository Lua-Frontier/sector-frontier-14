/*
 * LuaWorld - This file is licensed under AGPLv3
 * Copyright (c) 2025 LuaWorld Contributors
 * See AGPLv3.txt for details.
 */
using System;
using System.Collections.Generic;
using Content.Shared._NF.Finance;
using Content.Shared._Lua.Finance.Events;
using Content.Server._NF.Bank;
using Robust.Shared.IoC;
using Robust.Shared.Timing;
using Robust.Shared.Random;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Log;
using Content.Server.Preferences.Managers;
using Content.Shared.Preferences;

namespace Content.Server._NF.Finance;

/// <summary>
/// Centralized finance service that tracks per-user due (arrears) and hold, and enforces
/// available balance rules across deposits and withdrawals.
/// </summary>
public sealed partial class FinanceSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;

    private ISawmill _log = Logger.GetSawmill("finance");

    public readonly record struct LoanSnapshot(NetUserId UserId, int Principal, int Outstanding);
    public readonly record struct DepositSnapshot(NetUserId UserId, int Id, int Principal, int Accrued, TimeSpan NextCapAt, TimeSpan HardStopAt, DepositRateModel RateModel);
    public readonly record struct CharacterKey(NetUserId UserId, int SlotIndex);

    private readonly Dictionary<NetUserId, FinanceAccount> _accounts = new();
    private readonly Dictionary<NetUserId, List<Loan>> _loansByUser = new();
    private readonly Dictionary<NetUserId, TimeSpan> _nextChargeByUser = new();
    private readonly Dictionary<NetUserId, int> _missedCharges = new();
    private readonly Dictionary<CharacterKey, List<Deposit>> _depositsByCharacter = new();
    private readonly Dictionary<CharacterKey, int> _nextDepositId = new();

    private float _currentShiftInterestPercent;
    private TimeSpan _nextInterestStepAt;
    private float _currentDepositShiftInterestPercent;
    private TimeSpan _nextDepositDriftAt;
    private TimeSpan _nextFinanceTickAt;

    private FinanceAccount GetOrCreate(NetUserId userId)
    {
        if (!_accounts.TryGetValue(userId, out var acc))
        {
            acc = new FinanceAccount();
            _accounts[userId] = acc;
        }
        return acc;
    }

    private bool TryGetCharacterKey(ICommonSession session, out CharacterKey key)
    {
        key = default;
        try
        {
            if (!_prefs.TryGetCachedPreferences(session.UserId, out var prefs))
                return false;
            if (prefs.SelectedCharacter is not HumanoidCharacterProfile profile)
                return false;
            var index = prefs.IndexOfCharacter(profile);
            if (index == -1)
                return false;
            key = new CharacterKey(session.UserId, index);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public List<LoanSnapshot> GetAllLoansSnapshot()
    {
        var list = new List<LoanSnapshot>();
        foreach (var (userId, loans) in _loansByUser)
        {
            foreach (var l in loans)
            {
                if (!l.Active || l.Outstanding <= 0)
                    continue;
                list.Add(new LoanSnapshot(userId, l.Principal, l.Outstanding));
            }
        }
        return list;
    }

    public List<DepositSnapshot> GetAllDepositsSnapshot()
    {
        var list = new List<DepositSnapshot>();
        foreach (var (ckey, deps) in _depositsByCharacter)
        {
            foreach (var d in deps)
            {
                if (!d.Active)
                    continue;
                list.Add(new DepositSnapshot(ckey.UserId, d.Id, d.Principal, d.AccruedInterest, d.NextCapAt, d.HardStopAt, d.RateModel));
            }
        }
        return list;
    }

    public int GetOutstandingTotal(NetUserId userId)
    {
        if (!_loansByUser.TryGetValue(userId, out var loans) || loans.Count == 0)
            return 0;
        var sum = 0;
        foreach (var l in loans)
        {
            if (!l.Active || l.Outstanding <= 0)
                continue;
            sum += l.Outstanding;
        }
        return sum;
    }

    public int TryReduceOutstanding(NetUserId userId, int amount)
    {
        if (amount <= 0)
            return 0;
        if (!_loansByUser.TryGetValue(userId, out var loans) || loans.Count == 0)
            return 0;
        var remaining = amount;
        foreach (var l in loans)
        {
            if (!l.Active || l.Outstanding <= 0)
                continue;
            var pay = int.Min(remaining, l.Outstanding);
            l.Outstanding -= pay;
            remaining -= pay;
            if (remaining <= 0)
                break;
        }
        return amount - remaining;
    }

    public int TryEvenRepayLoans(NetUserId userId, int amount)
    {
        if (amount <= 0)
            return 0;
        if (!_loansByUser.TryGetValue(userId, out var loans) || loans.Count == 0)
            return 0;
        // Build active loans list
        var act = new List<Loan>();
        foreach (var l in loans)
            if (l.Active && l.Outstanding > 0)
                act.Add(l);
        if (act.Count == 0)
            return 0;
        var remaining = amount;
        // Even round-robin repayment: each pass pays up to floor(remaining/actives)
        while (remaining > 0 && act.Count > 0)
        {
            var per = Math.Max(1, remaining / act.Count);
            for (var i = act.Count - 1; i >= 0; i--)
            {
                var l = act[i];
                var pay = int.Min(per, l.Outstanding);
                l.Outstanding -= pay;
                remaining -= pay;
                if (l.Outstanding <= 0)
                    act.RemoveAt(i);
                if (remaining <= 0)
                    break;
            }
        }
        return amount - remaining;
    }

    private T GetCVar<T>(CVarDef<T> def) where T : notnull
    {
        try
        {
            return _cfg.GetCVar(def);
        }
        catch (InvalidConfigurationException)
        {
            return def.DefaultValue;
        }
    }

    public (int due, int hold) GetDueAndHold(NetUserId userId)
    {
        if (_accounts.TryGetValue(userId, out var acc))
            return (acc.Due, acc.Hold);
        return (0, 0);
    }

    public TimeSpan? GetNextChargeAt(NetUserId userId)
    {
        return _nextChargeByUser.TryGetValue(userId, out var t) ? t : null;
    }

    /// <summary>
    /// Applies incoming deposit to outstanding Due first, then Hold, then returns remainder to be deposited to bank balance.
    /// </summary>
    /// <param name="session">Player session</param>
    /// <param name="amount">Incoming amount, must be positive</param>
    /// <returns>Tuple appliedToDue, appliedToHold, remainderToDeposit</returns>
    public (int toDue, int toHold, int remainder) ApplyDepositPriority(ICommonSession session, int amount)
    {
        if (amount <= 0)
            return (0, 0, 0);

        var acc = GetOrCreate(session.UserId);

        var appliedToDue = 0;
        if (acc.Due > 0)
        {
            var pay = int.Min(amount, acc.Due);
            acc.Due -= pay;
            amount -= pay;
            appliedToDue = pay;
        }

        var appliedToHold = 0;
        if (amount > 0 && acc.Hold > 0)
        {
            var pay = int.Min(amount, acc.Hold);
            acc.Hold -= pay;
            amount -= pay;
            appliedToHold = pay;
        }

        return (appliedToDue, appliedToHold, amount);
    }

    /// <summary>
    /// Computes whether withdrawal can proceed given current character bank balance.
    /// </summary>
    public bool CanWithdraw(ICommonSession session, int currentBankBalance, int amount)
    {
        if (amount <= 0)
            return false;
        var (due, hold) = GetDueAndHold(session.UserId);
        var available = currentBankBalance - due - hold;
        return available >= amount && available >= 0;
    }

    /// <summary>
    /// Increases the outstanding Due by the specified amount.
    /// </summary>
    public void AddDue(NetUserId userId, int amount)
    {
        if (amount <= 0)
            return;
        var acc = GetOrCreate(userId);
        acc.Due = int.Max(0, acc.Due + amount);
    }

    /// <summary>
    /// Pays down outstanding Due by the specified amount.
    /// </summary>
    public int PayDue(NetUserId userId, int amount)
    {
        if (amount <= 0)
            return 0;
        var acc = GetOrCreate(userId);
        var pay = int.Min(amount, acc.Due);
        acc.Due -= pay;
        return pay;
    }

    /// <summary>
    /// Sets or adjusts Hold (reservation). Value cannot be negative.
    /// </summary>
    public void SetHold(NetUserId userId, int amount)
    {
        var acc = GetOrCreate(userId);
        acc.Hold = int.Max(0, amount);
    }

    private sealed class FinanceAccount
    {
        public int Due;
        public int Hold;
    }

    private sealed class Loan
    {
        public int Principal;
        public int Outstanding;
        public int AccruedInterestThisShift;
        public int AccruedPenaltyThisShift;
        public bool Active = true;
    }

    private sealed class Deposit
    {
        public int Id;
        public int Principal;
        public int AccruedInterest;
        public bool Active = true;
        public TimeSpan OpenedAt;
        public TimeSpan HardStopAt;
        public TimeSpan NextCapAt;
        public int PeriodMinutes;
        public DepositRateModel RateModel;
        public DepositTermType TermType;
        public float FixedAprPercent;
    }

    private readonly Dictionary<NetUserId, Queue<(TimeSpan Time, int Amount)>> _ops10m = new();
    private readonly Dictionary<NetUserId, Queue<(TimeSpan Time, int Amount)>> _ops1h = new();

    private bool CheckAndRecordOpsLimit(NetUserId userId, int amount)
    {
        var now = _timing.CurTime;
        var maxOps = GetCVar(FinanceCVars.DepositOpsMaxPer10Min);
        var maxSum = GetCVar(FinanceCVars.DepositOpsMaxSumPerHour);

        if (!_ops10m.TryGetValue(userId, out var q10))
            _ops10m[userId] = q10 = new();
        while (q10.Count > 0 && (now - q10.Peek().Time) >= TimeSpan.FromMinutes(10))
            q10.Dequeue();
        if (q10.Count >= maxOps)
            return false;

        if (!_ops1h.TryGetValue(userId, out var q60))
            _ops1h[userId] = q60 = new();
        while (q60.Count > 0 && (now - q60.Peek().Time) >= TimeSpan.FromHours(1))
            q60.Dequeue();
        var sum = 0;
        foreach (var e in q60)
            sum += e.Amount;
        if (sum + amount > maxSum)
            return false;

        q10.Enqueue((now, amount));
        q60.Enqueue((now, amount));
        return true;
    }

    public override void Initialize()
    {
        base.Initialize();
        _currentShiftInterestPercent = GetCVar(FinanceCVars.CreditShiftInterestPercent);
        ScheduleNextInterestStep();
        _currentDepositShiftInterestPercent = GetCVar(FinanceCVars.DepositShiftInterestPercent);
        ScheduleNextDepositDrift();
        _nextFinanceTickAt = _timing.CurTime; // allow immediate first run
                                              // Hook up console/BUI handlers in partial implementation
        InitializeConsoles();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        // Throttle heavy finance processing to reduce server load
        if (now < _nextFinanceTickAt)
            return;
        _nextFinanceTickAt = now + TimeSpan.FromSeconds(1);

        // Update shift interest drift
        if (now >= _nextInterestStepAt)
            StepShiftInterest();

        // Update deposit drift if enabled
        if (GetCVar(FinanceCVars.DepositEnableDrift) && now >= _nextDepositDriftAt)
            StepDepositDrift();

        // Per-user hold recompute and autopay
        foreach (var session in _players.Sessions)
        {
            var userId = session.UserId;
            if (!_loansByUser.TryGetValue(userId, out var list) || list.Count == 0)
                goto Deposits; // still need deposits path

            // Compute total hourly interest hold
            var hourlyRate = GetCurrentHourlyInterestPercent();
            var hold = 0;
            foreach (var loan in list)
            {
                if (!loan.Active || loan.Outstanding <= 0)
                    continue;
                var interest = (int)MathF.Ceiling(loan.Outstanding * (hourlyRate / 100f));
                hold += interest;
            }
            SetHold(userId, hold);

            // Skip autopay if no attached entity (offline)
            if (session.AttachedEntity is not { Valid: true } mob)
                goto Deposits;

            // Ensure schedule
            if (!_nextChargeByUser.TryGetValue(userId, out var nextAt))
                _nextChargeByUser[userId] = ComputeNextChargeTime(now);
            else if (now >= nextAt)
            {
                // Charge once for this hour window: pay 'hold' amount as interest
                var interestToPay = hold;

                // Apply shift caps per loan for interest and penalty
                // Interest cap handled implicitly by hold sum; penalty cap applied when computing penalty

                var success = _bank.TryBankWithdraw(mob, interestToPay);
                if (!success)
                {
                    // Compute penalty = sum over loans of ceil(outstanding * lateFee%/hour)
                    var lateFeePercent = GetCVar(FinanceCVars.CreditLateFeePercentPerHour);
                    var penalty = 0;
                    foreach (var loan in list)
                    {
                        if (!loan.Active || loan.Outstanding <= 0)
                            continue;
                        var p = (int)MathF.Ceiling(loan.Outstanding * (lateFeePercent / 100f));
                        // Enforce per-shift penalty cap
                        var penaltyCapShift = (int)MathF.Floor(loan.Principal * (GetCVar(FinanceCVars.CreditPenaltyCapPercentPerShift) / 100f));
                        var remainingPenaltyCap = Math.Max(0, penaltyCapShift - loan.AccruedPenaltyThisShift);
                        p = Math.Min(p, remainingPenaltyCap);
                        loan.AccruedPenaltyThisShift += p;
                        penalty += p;
                    }

                    var totalCapShift = (int)MathF.Floor(GetTotalPrincipal(list) * (GetCVar(FinanceCVars.CreditTotalCapPercentPerShift) / 100f));
                    var accruedThisShift = GetAccruedInterest(list) + GetAccruedPenalty(list);
                    var remainingTotalCap = Math.Max(0, totalCapShift - accruedThisShift);
                    var totalDueNow = Math.Min(interestToPay + penalty, remainingTotalCap);

                    AddDue(userId, totalDueNow); //Lua: add late fee + interest into Due respecting caps

                    // Default logic: if подряд X часов не удаётся списать проценты — учитываем дефолт
                    var defaultHours = GetCVar(FinanceCVars.CreditDefaultAfterConsecutiveLateHours);
                    if (defaultHours > 0)
                    {
                        if (!_missedCharges.TryGetValue(userId, out var miss)) miss = 0;
                        miss++;
                        _missedCharges[userId] = miss;
                        if (miss >= defaultHours)
                        {
                            // При дефолте деактивируем кредиты (перестаём начислять/держать) и конвертируем остаток в Due
                            var remain = 0;
                            foreach (var loan in list)
                            {
                                if (!loan.Active) continue;
                                remain += Math.Max(0, loan.Outstanding);
                                loan.Active = false;
                            }
                            if (remain > 0)
                                AddDue(userId, remain);
                            // После дефолта держать проценты больше не нужно
                            SetHold(userId, 0);
                        }
                    }
                }
                else
                {
                    // Mark interest accrued this shift
                    foreach (var loan in list)
                    {
                        if (!loan.Active || loan.Outstanding <= 0)
                            continue;
                        var portion = (int)MathF.Ceiling(loan.Outstanding * (hourlyRate / 100f));
                        loan.AccruedInterestThisShift += portion;
                    }
                    // Сбрасываем счётчик пропусков списания
                    _missedCharges[userId] = 0;
                }

                _nextChargeByUser[userId] = ComputeNextChargeTime(now);
            }

        Deposits:
            // Process deposits capitalization (online) for current character only
            if (TryGetCharacterKey(session, out var ckey) && _depositsByCharacter.TryGetValue(ckey, out var deps) && deps.Count > 0)
            {
                var minCap = GetCVar(FinanceCVars.DepositCapitalizationMinMinutes);
                var maxCap = GetCVar(FinanceCVars.DepositCapitalizationMaxMinutes);
                foreach (var dep in deps)
                {
                    if (!dep.Active)
                        continue;
                    if (now >= dep.HardStopAt)
                        continue; // no more growth after stop
                    if (now < dep.NextCapAt)
                        continue;

                    // Capitalize interest for this period
                    var periodHours = dep.PeriodMinutes / 60f;
                    var aprShift = GetDepositAprShiftPercent(dep, now);
                    var hourly = aprShift / 8f;
                    var rate = hourly * periodHours;
                    var baseAmount = dep.Principal + dep.AccruedInterest;
                    var add = (int)MathF.Ceiling(baseAmount * (rate / 100f));
                    if (add > 0)
                        dep.AccruedInterest += add;

                    // schedule next
                    var nextPeriod = _random.Next(minCap, maxCap + 1);
                    dep.PeriodMinutes = nextPeriod;
                    dep.NextCapAt = now + TimeSpan.FromMinutes(nextPeriod);
                }
            }
        }
    }

    private int GetTotalPrincipal(List<Loan> list)
    {
        var sum = 0;
        foreach (var l in list)
            sum += l.Principal;
        return sum;
    }

    private int GetAccruedInterest(List<Loan> list)
    {
        var sum = 0;
        foreach (var l in list)
            sum += l.AccruedInterestThisShift;
        return sum;
    }

    private int GetAccruedPenalty(List<Loan> list)
    {
        var sum = 0;
        foreach (var l in list)
            sum += l.AccruedPenaltyThisShift;
        return sum;
    }

    private void ScheduleNextInterestStep()
    {
        var now = _timing.CurTime;
        var minutes = _random.Next(30, 91); // 30-90 minutes
        _nextInterestStepAt = now + TimeSpan.FromMinutes(minutes);
    }

    private void StepShiftInterest()
    {
        var basePct = GetCVar(FinanceCVars.CreditShiftInterestPercent);
        var maxPct = GetCVar(FinanceCVars.CreditShiftInterestMaxPercent);
        // Step by -2, 0, or +2 toward bounds
        var step = _random.Pick(new[] { -2f, 0f, 2f });
        _currentShiftInterestPercent = MathF.Max(basePct, MathF.Min(maxPct, _currentShiftInterestPercent + step));
        ScheduleNextInterestStep();
    }

    private float GetCurrentHourlyInterestPercent()
    {
        // Convert current shift percent (per 8 hours) to hourly percent.
        return _currentShiftInterestPercent / 8f;
    }

    private void ScheduleNextDepositDrift()
    {
        var now = _timing.CurTime;
        var min = GetCVar(FinanceCVars.DepositDriftMinMinutes);
        var max = GetCVar(FinanceCVars.DepositDriftMaxMinutes);
        _nextDepositDriftAt = now + TimeSpan.FromMinutes(_random.Next(min, max + 1));
    }

    private void StepDepositDrift()
    {
        var basePct = GetCVar(FinanceCVars.DepositShiftInterestPercent);
        var maxPct = GetCVar(FinanceCVars.DepositShiftInterestMaxPercent);
        var step = _random.Pick(new[] { -1f, 0f, 1f });
        _currentDepositShiftInterestPercent = MathF.Max(basePct, MathF.Min(maxPct, _currentDepositShiftInterestPercent + step));
        ScheduleNextDepositDrift();
    }

    private float GetDepositAprShiftPercent(Deposit dep, TimeSpan now)
    {
        switch (dep.RateModel)
        {
            case DepositRateModel.FixedApr:
                return dep.FixedAprPercent;
            case DepositRateModel.FloatingApr:
                {
                    var creditShift = _currentShiftInterestPercent;
                    var spread = GetCVar(FinanceCVars.DepositBankSpreadPercent);
                    var basePct = MathF.Max(0f, creditShift - spread);
                    var cap = GetCVar(FinanceCVars.DepositShiftInterestMaxPercent);
                    return MathF.Min(basePct, cap);
                }
            case DepositRateModel.ProgressiveApr:
            default:
                {
                    var baseFixed = dep.FixedAprPercent;
                    var bonusMax = GetCVar(FinanceCVars.DepositProgressiveBonusMaxPercent);
                    var total = (dep.HardStopAt - dep.OpenedAt).TotalMinutes;
                    var elapsed = Math.Max(0.0, (now - dep.OpenedAt).TotalMinutes);
                    var frac = total <= 0 ? 0f : (float)Math.Min(1.0, elapsed / total);
                    var apr = baseFixed + bonusMax * frac;
                    return apr;
                }
        }
    }

    private TimeSpan ComputeNextChargeTime(TimeSpan now)
    {
        var periodMin = GetCVar(FinanceCVars.CreditAutopayPeriodMinutes);
        var jitter = GetCVar(FinanceCVars.CreditAutopayJitterMinutes);
        var offset = _random.Next(-jitter, jitter + 1);
        return now + TimeSpan.FromMinutes(periodMin + offset);
    }

    public bool TryCreateLoan(ICommonSession session, int principal)
    {
        if (principal <= 0)
            return false;
        var maxPerLoan = GetCVar(FinanceCVars.CreditMaxPrincipalPerLoan);
        if (principal > maxPerLoan)
            return false;

        // Enforce playtime
        if (!MeetsMinPlaytime(session))
            return false;

        var userId = session.UserId;
        if (!_loansByUser.TryGetValue(userId, out var list))
        {
            list = new List<Loan>();
            _loansByUser[userId] = list;
        }

        // Enforce per-user limits (second loan requires score)
        var activeLoans = 0;
        foreach (var l in list)
            if (l.Active && l.Outstanding > 0)
                activeLoans++;

        if (activeLoans >= 1)
        {
            var score = CalculateScore(session);
            var threshold = GetCVar(FinanceCVars.CreditSecondLoanScoreThreshold);
            if (activeLoans >= 2 || score < threshold)
                return false;
            // Disallow if there is outstanding due/penalty
            var (due, _) = GetDueAndHold(userId);
            if (due > 0)
                return false;
        }

        // Enforce max loan by score/baseline and cap by per-loan limit (+ debt cap multiplier)
        var maxAllowed = CalculateMaxLoan(session);
        var totalOutstanding = GetOutstandingTotal(userId);
        var debtCapMul = GetCVar(FinanceCVars.CreditDebtCapMultiplier);
        var debtCap = (int)MathF.Floor(maxAllowed * debtCapMul);
        if (totalOutstanding + principal > debtCap)
            return false;
        if (principal > maxAllowed)
            return false;

        // Credit loan principal to player's bank account immediately upon issuance.
        // Requires an attached entity to perform the bank transaction.
        if (session.AttachedEntity is not { Valid: true } mob)
            return false;
        if (!_bank.TryBankDeposit(mob, principal))
            return false;

        list.Add(new Loan
        {
            Principal = principal,
            Outstanding = principal,
            AccruedInterestThisShift = 0,
            AccruedPenaltyThisShift = 0,
            Active = true
        });
        return true;
    }

    private bool MeetsMinPlaytime(ICommonSession session)
    {
        // Use playtime manager if available on server side.
        // Fallback: allow.
        try
        {
            var man = IoCManager.Resolve<Content.Shared.Players.PlayTimeTracking.ISharedPlaytimeManager>();
            var times = man.GetPlayTimes(session);
            var mins = GetCVar(FinanceCVars.CreditMinPlaytimeMinutes);
            if (!times.TryGetValue(Content.Shared.Players.PlayTimeTracking.PlayTimeTrackingShared.TrackerOverall, out var total))
                return false;
            return total.TotalMinutes >= mins;
        }
        catch
        {
            return true;
        }
    }

    public int CalculateScore(ICommonSession session)
    {
        // Playtime contribution (0..50)
        float halfSat = GetCVar(FinanceCVars.ScorePlaytimeHalfSaturationHours);
        var man = IoCManager.Resolve<Content.Shared.Players.PlayTimeTracking.ISharedPlaytimeManager>();
        var times = man.GetPlayTimes(session);
        times.TryGetValue(Content.Shared.Players.PlayTimeTracking.PlayTimeTrackingShared.TrackerOverall, out var total);
        var hours = (float)total.TotalHours;
        var playScore = 50f * (hours / (hours + halfSat));

        // Roles contribution (0..cap)
        float roleThresh = GetCVar(FinanceCVars.ScoreRoleThresholdHours);
        float perRole = GetCVar(FinanceCVars.ScoreRolePointsPerRole);
        float roleCap = GetCVar(FinanceCVars.ScoreRolePointsCap);
        float roleScore = 0f;
        foreach (var (tracker, span) in times)
        {
            if (tracker == Content.Shared.Players.PlayTimeTracking.PlayTimeTrackingShared.TrackerOverall)
                continue;
            if (span.TotalHours >= roleThresh)
                roleScore += perRole;
            if (roleScore >= roleCap)
            {
                roleScore = roleCap;
                break;
            }
        }

        // History contribution placeholder (0..30). For now assume neutral 15.
        float historyScore = 15f;

        var score = (int)MathF.Round(MathF.Min(100f, MathF.Max(0f, playScore + roleScore + historyScore)));
        return score;
    }

    public int CalculateMaxLoan(ICommonSession session)
    {
        var baseline = GetCVar(FinanceCVars.BaselineShipPrice);
        var score = CalculateScore(session);
        var minMul = 0.25f;
        var maxMul = 1.0f;
        var mul = minMul + (maxMul - minMul) * (score / 100f);
        var limit = (int)MathF.Floor(baseline * mul);
        var cap = GetCVar(FinanceCVars.CreditMaxPrincipalPerLoan);
        return Math.Min(limit, cap);
    }

    public int GetActiveLoansCount(NetUserId userId)
    {
        if (!_loansByUser.TryGetValue(userId, out var list))
            return 0;
        var n = 0;
        foreach (var l in list)
            if (l.Active && l.Outstanding > 0)
                n++;
        return n;
    }

    public bool TryOpenDeposit(ICommonSession session, int amount, DepositTermType term, DepositRateModel rate, out int depositId)
    {
        depositId = 0;
        if (session.AttachedEntity is not { Valid: true } mob)
            return false;
        if (amount <= 0)
            return false;
        var minAmt = GetCVar(FinanceCVars.DepositMinAmount);
        var maxAmt = GetCVar(FinanceCVars.DepositMaxAmount);
        if (amount < minAmt || amount > maxAmt)
            return false;

        var userId = session.UserId;
        if (!CheckAndRecordOpsLimit(userId, amount))
            return false;

        if (!TryGetCharacterKey(session, out var ckey))
            return false;
        if (!_depositsByCharacter.TryGetValue(ckey, out var list))
        {
            list = new List<Deposit>();
            _depositsByCharacter[ckey] = list;
        }
        var maxActive = GetCVar(FinanceCVars.DepositMaxActivePerUser);
        var activeCount = 0;
        var totalPrincipal = 0;
        foreach (var d in list)
        {
            if (d.Active)
                activeCount++;
            totalPrincipal += d.Principal;
        }
        if (activeCount >= maxActive)
            return false;
        var maxTotalPrincipal = GetCVar(FinanceCVars.DepositMaxTotalPrincipalPerUser);
        if (totalPrincipal + amount > maxTotalPrincipal)
            return false;

        // Fees
        var openFeePct = GetCVar(FinanceCVars.DepositOpenFeePercent);
        var fee = (int)MathF.Ceiling(amount * (openFeePct / 100f));
        var required = amount + fee;

        // Pre-check available considering due/hold
        if (!_bank.TryGetBalance(session, out var balance))
        {
            _log.Warning($"DepositOpen balance check failed for user={userId}");
            return false;
        }
        if (!CanWithdraw(session, balance, required))
        {
            _log.Warning($"DepositOpen insufficient available funds user={userId} balance={balance} required={required}");
            return false;
        }

        // Withdraw funds
        if (!_bank.TryBankWithdraw(mob, required))
        {
            _log.Warning($"DepositOpen withdraw failed user={userId} required={required}");
            return false;
        }

        var nextId = _nextDepositId.TryGetValue(ckey, out var nid) ? nid : 1;
        var minCap = GetCVar(FinanceCVars.DepositCapitalizationMinMinutes);
        var maxCap = GetCVar(FinanceCVars.DepositCapitalizationMaxMinutes);
        var stopMinutes = GetCVar(FinanceCVars.DepositCapitalizationHardStopMinutes);
        var termMin = term == DepositTermType.Long
            ? GetCVar(FinanceCVars.DepositLongMinMinutes)
            : GetCVar(FinanceCVars.DepositShortMinMinutes);
        var period = _random.Next(minCap, maxCap + 1);
        var now = _timing.CurTime;
        var dep = new Deposit
        {
            Id = nextId,
            Principal = amount,
            AccruedInterest = 0,
            Active = true,
            OpenedAt = now,
            HardStopAt = now + TimeSpan.FromMinutes(Math.Max(stopMinutes, termMin)),
            PeriodMinutes = period,
            NextCapAt = now + TimeSpan.FromMinutes(period),
            RateModel = rate,
            TermType = term,
            FixedAprPercent = GetCVar(FinanceCVars.DepositFixedAprShiftPercent)
        };
        list.Add(dep);
        _nextDepositId[ckey] = nextId + 1;
        depositId = dep.Id;
        _log.Info($"DepositOpen user={userId} id={dep.Id} amount={amount} term={term} rate={rate} fee={fee}");
        return true;
    }

    public bool TryTopUpDeposit(ICommonSession session, int depositId, int amount)
    {
        if (session.AttachedEntity is not { Valid: true } mob)
            return false;
        if (amount <= 0)
            return false;
        var minAmt = GetCVar(FinanceCVars.DepositMinAmount);
        if (amount < minAmt)
            return false;
        var userId = session.UserId;
        if (!CheckAndRecordOpsLimit(userId, amount))
            return false;
        if (!TryGetCharacterKey(session, out var ckey) || !_depositsByCharacter.TryGetValue(ckey, out var list))
            return false;
        var dep = list.Find(d => d.Id == depositId && d.Active);
        if (dep == null)
            return false;

        // Fee on top-up reuses open fee
        var feePct = GetCVar(FinanceCVars.DepositOpenFeePercent);
        var fee = (int)MathF.Ceiling(amount * (feePct / 100f));
        var required = amount + fee;
        if (!_bank.TryBankWithdraw(mob, required))
            return false;
        dep.Principal = int.Max(0, dep.Principal + amount);
        _log.Info($"DepositTopUp user={userId} id={depositId} amount={amount} fee={fee}");
        return true;
    }

    public bool TryPartialWithdrawDeposit(ICommonSession session, int depositId, int amount)
    {
        if (session.AttachedEntity is not { Valid: true } mob)
            return false;
        if (amount <= 0)
            return false;
        var userId = session.UserId;
        if (!CheckAndRecordOpsLimit(userId, amount))
            return false;
        if (!TryGetCharacterKey(session, out var ckey) || !_depositsByCharacter.TryGetValue(ckey, out var list))
            return false;
        var dep = list.Find(d => d.Id == depositId && d.Active);
        if (dep == null)
            return false;

        // Allow withdrawing only from accrued interest (no principal touch)
        var take = Math.Min(amount, dep.AccruedInterest);
        if (take <= 0)
            return false;
        dep.AccruedInterest -= take;
        var ok = _bank.TryBankDeposit(mob, take);
        if (ok)
            _log.Info($"DepositPartWithdraw user={userId} id={depositId} amount={take}");
        return ok;
    }

    public bool TryCloseDeposit(ICommonSession session, int depositId, out int payout, out int penalty)
    {
        payout = 0;
        penalty = 0;
        if (session.AttachedEntity is not { Valid: true } mob)
            return false;
        var userId = session.UserId;
        if (!TryGetCharacterKey(session, out var ckey) || !_depositsByCharacter.TryGetValue(ckey, out var list))
            return false;
        var dep = list.Find(d => d.Id == depositId && d.Active);
        if (dep == null)
            return false;

        var now = _timing.CurTime;
        var total = dep.Principal + dep.AccruedInterest;

        // Enforce close lock window, if configured
        var closeLock = GetCVar(FinanceCVars.DepositCloseLockMinutes);
        if (closeLock > 0)
        {
            var notBefore = dep.OpenedAt + TimeSpan.FromMinutes(closeLock);
            if (now < notBefore)
                return false;
        }

        // Rate limit close operations using total amount as weight
        if (!CheckAndRecordOpsLimit(userId, total))
            return false;

        // Early withdrawal penalty on accrued interest only
        if (now < dep.HardStopAt && dep.AccruedInterest > 0)
        {
            var startPct = GetCVar(FinanceCVars.DepositEarlyPenaltyStartPercent);
            var t = (now - dep.OpenedAt).TotalMinutes;
            var T = (dep.HardStopAt - dep.OpenedAt).TotalMinutes;
            var frac = (float)Math.Clamp(t / T, 0.0, 1.0);
            var penaltyPct = startPct * MathF.Pow(1f - frac, 2f);
            penalty = (int)MathF.Floor(dep.AccruedInterest * (penaltyPct / 100f));
        }

        // Close fee
        var closeFeePct = GetCVar(FinanceCVars.DepositCloseFeePercent);
        var closeFee = (int)MathF.Ceiling(total * (closeFeePct / 100f));

        var credited = Math.Max(0, total - penalty - closeFee);

        // Compute application to Due/Hold without mutating first
        var acc = GetOrCreate(userId);
        var toDue = Math.Min(credited, acc.Due);
        var afterDue = credited - toDue;
        var toHold = Math.Min(afterDue, acc.Hold);
        var remainder = afterDue - toHold;

        // If there is a remainder to deposit, ensure the bank deposit succeeds first
        if (remainder > 0)
        {
            if (!_bank.TryBankDeposit(mob, remainder))
            {
                _log.Warning($"DepositClose bank deposit failed user={userId} remainder={remainder}");
                return false;
            }
        }

        // Commit state changes only after successful bank operations
        acc.Due -= toDue;
        acc.Hold -= toHold;
        dep.Active = false;

        payout = credited;
        _log.Info($"DepositClose user={userId} id={depositId} payout={payout} penalty={penalty} closeFee={closeFee} toDue={toDue} toHold={toHold} remainder={remainder}");
        return true;
    }

    public FinanceDepositRow[] BuildDepositRows(ICommonSession session)
    {
        if (!TryGetCharacterKey(session, out var ckey) || !_depositsByCharacter.TryGetValue(ckey, out var list) || list.Count == 0)
            return Array.Empty<FinanceDepositRow>();
        var now = _timing.CurTime;
        var rows = new List<FinanceDepositRow>(list.Count);
        foreach (var d in list)
        {
            if (!d.Active)
                continue;
            var apr = GetDepositAprShiftPercent(d, now);
            var nextSec = (int)Math.Max(0, (d.NextCapAt - now).TotalSeconds);
            var stopSec = (int)Math.Max(0, (d.HardStopAt - now).TotalSeconds);
            var previewPenalty = 0;
            if (now < d.HardStopAt && d.AccruedInterest > 0)
            {
                var startPct = GetCVar(FinanceCVars.DepositEarlyPenaltyStartPercent);
                var t = (now - d.OpenedAt).TotalMinutes;
                var T = (d.HardStopAt - d.OpenedAt).TotalMinutes;
                var frac = (float)Math.Clamp(t / T, 0.0, 1.0);
                var penaltyPct = startPct * MathF.Pow(1f - frac, 2f);
                previewPenalty = (int)MathF.Floor(d.AccruedInterest * (penaltyPct / 100f));
            }
            rows.Add(new FinanceDepositRow(d.Id, d.Principal, d.AccruedInterest, d.RateModel, apr, nextSec, stopSec, previewPenalty));
        }
        return rows.ToArray();
    }
}


