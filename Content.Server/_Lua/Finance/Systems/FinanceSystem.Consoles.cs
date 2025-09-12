/*
 * LuaWorld - This file is licensed under AGPLv3
 * Copyright (c) 2025 LuaWorld Contributors
 * See AGPLv3.txt for details.
 */
using Content.Server._NF.Finance.Components;
using Content.Shared._NF.Bank.Components;
using Content.Shared._Lua.Finance.BUI;
using Content.Shared._Lua.Finance.Events;
using Content.Shared._NF.Finance;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Network;

namespace Content.Server._NF.Finance;

public sealed partial class FinanceSystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    private void InitializeConsoles()
    {
        // Deposit console
        SubscribeLocalEvent<FinanceDepositConsoleComponent, BoundUIOpenedEvent>(OnDepositUiOpen);
        SubscribeLocalEvent<FinanceDepositConsoleComponent, FinanceStatusQueryMessage>(OnDepositStatusQuery);
        SubscribeLocalEvent<FinanceDepositConsoleComponent, FinanceDepositListQueryMessage>(OnDepositListQuery);
        SubscribeLocalEvent<FinanceDepositConsoleComponent, FinanceOpenDepositRequestMessage>(OnDepositOpenRequest);
        SubscribeLocalEvent<FinanceDepositConsoleComponent, FinanceCloseDepositRequestMessage>(OnDepositCloseRequest);
        SubscribeLocalEvent<FinanceDepositConsoleComponent, FinanceTopUpDepositRequestMessage>(OnDepositTopUpRequest);
        SubscribeLocalEvent<FinanceDepositConsoleComponent, FinancePartialWithdrawDepositRequestMessage>(OnDepositPartialWithdrawRequest);

        // Rating console
        SubscribeLocalEvent<FinanceRatingConsoleComponent, BoundUIOpenedEvent>(OnRatingUiOpen);
        SubscribeLocalEvent<FinanceRatingConsoleComponent, FinanceRatingQueryMessage>(OnRatingQuery);

        // Issuance console
        SubscribeLocalEvent<FinanceIssuanceConsoleComponent, BoundUIOpenedEvent>(OnIssuanceUiOpen);
        SubscribeLocalEvent<FinanceIssuanceConsoleComponent, FinanceIssueLoanRequestMessage>(OnIssueLoanRequest);
        // Issuance window also requests rating info; handle the shared rating query here, too.
        SubscribeLocalEvent<FinanceIssuanceConsoleComponent, FinanceRatingQueryMessage>(OnIssuanceRatingQuery);

        // Loans/deposits overview consoles
        SubscribeLocalEvent<FinanceLoansConsoleComponent, BoundUIOpenedEvent>(OnLoansUiOpen);
        SubscribeLocalEvent<FinanceLoansConsoleComponent, FinanceLoansQueryMessage>(OnLoansQuery);
        SubscribeLocalEvent<FinanceLoansConsoleComponent, FinanceDepositsQueryMessage>(OnDepositsOverviewQuery);
    }

    #region Deposit console

    private void OnDepositUiOpen(EntityUid uid, FinanceDepositConsoleComponent comp, BoundUIOpenedEvent args)
    {
        if (args.Actor is not { Valid: true } player)
            return;
        SendDepositStatus(uid, player);
        SendDepositList(uid, player);
    }

    private void OnDepositStatusQuery(EntityUid uid, FinanceDepositConsoleComponent comp, FinanceStatusQueryMessage args)
    {
        if (args.Actor is not { Valid: true } player)
            return;
        SendDepositStatus(uid, player);
    }

    private void OnDepositListQuery(EntityUid uid, FinanceDepositConsoleComponent comp, FinanceDepositListQueryMessage args)
    {
        if (args.Actor is not { Valid: true } player)
            return;
        SendDepositList(uid, player);
    }

    private void OnDepositOpenRequest(EntityUid uid, FinanceDepositConsoleComponent comp, FinanceOpenDepositRequestMessage args)
    {
        if (args.Actor is not { Valid: true } player)
            return;
        if (TryOpenDeposit(GetSession(player), args.Amount, args.Term, args.Rate, out _))
        {
            SendDepositStatus(uid, player);
            SendDepositList(uid, player);
        }
    }

    private void OnDepositTopUpRequest(EntityUid uid, FinanceDepositConsoleComponent comp, FinanceTopUpDepositRequestMessage args)
    {
        if (args.Actor is not { Valid: true } player)
            return;
        if (TryTopUpDeposit(GetSession(player), args.DepositId, args.Amount))
        {
            SendDepositStatus(uid, player);
            SendDepositList(uid, player);
        }
    }

    private void OnDepositPartialWithdrawRequest(EntityUid uid, FinanceDepositConsoleComponent comp, FinancePartialWithdrawDepositRequestMessage args)
    {
        if (args.Actor is not { Valid: true } player)
            return;
        if (TryPartialWithdrawDeposit(GetSession(player), args.DepositId, args.Amount))
        {
            SendDepositStatus(uid, player);
            SendDepositList(uid, player);
        }
    }

    private void OnDepositCloseRequest(EntityUid uid, FinanceDepositConsoleComponent comp, FinanceCloseDepositRequestMessage args)
    {
        if (args.Actor is not { Valid: true } player)
            return;
        if (TryCloseDeposit(GetSession(player), args.DepositId, out _, out _))
        {
            SendDepositStatus(uid, player);
            SendDepositList(uid, player);
        }
    }

    private void SendDepositStatus(EntityUid console, EntityUid player)
    {
        if (!_bank.TryGetBalance(player, out var balance))
            balance = 0;
        var (due, hold) = GetDueAndHold(GetNetUser(player));
        var available = int.Max(0, balance - due - hold);
        var nextEta = GetNextChargeAt(GetNetUser(player));
        var min = GetCVar(FinanceCVars.DepositMinAmount);
        var max = GetCVar(FinanceCVars.DepositMaxAmount);
        var step = GetCVar(FinanceCVars.DepositStepAmount);
        var state = new FinanceStatusState(balance, available, due, hold,
            nextEta.HasValue ? nextEta.Value - _gameTiming.CurTime : null,
            min, max, step);
        _ui.SetUiState(console, NFFinanceDepositUiKey.Key, state);
    }

    private void SendDepositList(EntityUid console, EntityUid player)
    {
        var rows = BuildDepositRows(GetSession(player));
        _ui.SetUiState(console, NFFinanceDepositUiKey.Key, new FinanceDepositListState(rows));
    }

    #endregion

    #region Rating console

    private void OnRatingUiOpen(EntityUid uid, FinanceRatingConsoleComponent comp, BoundUIOpenedEvent args)
    {
        if (args.Actor is not { Valid: true } player)
            return;
        SendRating(uid, player);
    }

    private void OnRatingQuery(EntityUid uid, FinanceRatingConsoleComponent comp, FinanceRatingQueryMessage args)
    {
        if (args.Actor is not { Valid: true } player)
            return;
        SendRating(uid, player, args.QueryName);
    }

    private FinanceRatingState BuildRatingState(EntityUid player, string? queryName = null)
    {
        var session = GetSession(player);
        var score = CalculateScore(session);
        // Optionally adjust by history window (placeholder usage to wire cvar)
        var histHours = GetCVar(FinanceCVars.ScoreHistoryWindowHours);
        _ = histHours; // reserved for future detailed history logic
        var maxLoan = CalculateMaxLoan(session);
        var active = GetActiveLoansCount(session.UserId);
        string? targetName = Name(player);
        var next = GetNextChargeAt(session.UserId);
        int? nextSec = next.HasValue ? (int?)Math.Max(0, (next.Value - _gameTiming.CurTime).TotalSeconds) : null;
        return new FinanceRatingState(queryName ?? string.Empty, score, maxLoan, active, targetName, nextSec);
    }

    private void SendRating(EntityUid console, EntityUid player, string? queryName = null)
    {
        var state = BuildRatingState(player, queryName);
        // If this is a rating console and has a target ID inserted, display that name for clarity
        if (TryComp<FinanceRatingConsoleComponent>(console, out var ratingComp) &&
            ratingComp.TargetIdSlot.ContainerSlot?.ContainedEntity is { Valid: true } targetId)
        {
            string? targetName = null;
            if (TryComp<Content.Shared.Access.Components.IdCardComponent>(targetId, out var id))
                targetName = id.FullName;
            if (string.IsNullOrWhiteSpace(targetName) && TryComp<MetaDataComponent>(targetId, out var meta))
                targetName = meta.EntityName;
            if (!string.IsNullOrWhiteSpace(targetName))
            {
                state = new FinanceRatingState(state.Query, state.Score, state.MaxLoan, state.ActiveLoans, targetName, state.NextChargeSeconds);
            }
        }
        _ui.SetUiState(console, NFFinanceRatingUiKey.Key, state);
    }

    private void SendIssuanceRating(EntityUid console, EntityUid player, string? queryName = null)
    {
        var state = BuildRatingState(player, queryName);
        // Show the name from privileged ID if present for issuer context
        if (TryComp<FinanceIssuanceConsoleComponent>(console, out var issueComp) &&
            issueComp.PrivilegedIdSlot.ContainerSlot?.ContainedEntity is { Valid: true } privId)
        {
            string? targetName = null;
            if (TryComp<Content.Shared.Access.Components.IdCardComponent>(privId, out var id))
                targetName = id.FullName;
            if (string.IsNullOrWhiteSpace(targetName) && TryComp<MetaDataComponent>(privId, out var meta))
                targetName = meta.EntityName;
            if (!string.IsNullOrWhiteSpace(targetName))
            {
                state = new FinanceRatingState(state.Query, state.Score, state.MaxLoan, state.ActiveLoans, targetName, state.NextChargeSeconds);
            }
        }
        _ui.SetUiState(console, NFFinanceIssuanceUiKey.Key, state);
    }

    #endregion

    #region Issuance console

    private void OnIssuanceUiOpen(EntityUid uid, FinanceIssuanceConsoleComponent comp, BoundUIOpenedEvent args)
    {
        if (args.Actor is not { Valid: true } player)
            return;
        // Prime rating panel in the issuance window (uses issuance UI key)
        SendIssuanceRating(uid, player);
    }

    private void OnIssuanceRatingQuery(EntityUid uid, FinanceIssuanceConsoleComponent comp, FinanceRatingQueryMessage args)
    {
        if (args.Actor is not { Valid: true } player)
            return;
        SendIssuanceRating(uid, player, args.QueryName);
    }

    private void OnIssueLoanRequest(EntityUid uid, FinanceIssuanceConsoleComponent comp, FinanceIssueLoanRequestMessage args)
    {
        if (args.Actor is not { Valid: true } player)
            return;
        // Require privileged Command ID card inserted into the console slot
        var ok = false;
        if (comp.PrivilegedIdSlot.Item is { Valid: true } idCard)
        {
            // Check that ID has Command access via AccessSystem by reading tags
            var accessSys = EntityManager.System<Content.Shared.Access.Systems.SharedAccessSystem>();
            var tags = accessSys.TryGetTags(idCard);
            if (tags != null)
            {
                foreach (var t in tags)
                {
                    if (t == "Command")
                    {
                        ok = true;
                        break;
                    }
                }
            }
            if (ok)
                ok = TryCreateLoan(GetSession(player), args.Amount);
        }
        string msg = ok ? "Кредит выдан" : "Отказано в выдаче";
        _ui.SetUiState(uid, NFFinanceIssuanceUiKey.Key, new FinanceIssueLoanResponseState(ok, msg));
    }

    #endregion

    #region Loans/deposits overview

    private void OnLoansUiOpen(EntityUid uid, FinanceLoansConsoleComponent _1, BoundUIOpenedEvent _2)
    {
        // Determine console role: if it's the public loans console -> loans only;
        // if it's the service deposits overview console -> deposits only with force close allowed.
        var showLoans = true;
        var showDeposits = false;
        var allowForceClose = false;
        // Default is loans console; no special tag handling required.
        if (MetaData(uid).EntityPrototype?.ID == "NFFinanceDepositsViewComputerService" || MetaData(uid).EntityPrototype?.ID == "NFFinanceDepositsViewComputer")
        {
            showLoans = false;
            showDeposits = true;
            allowForceClose = true;
        }
        _ui.SetUiState(uid, NFFinanceLoansUiKey.Key, new FinanceLoansConfigState(showLoans, showDeposits, allowForceClose));
        if (showLoans)
            SendLoans(uid);
        if (showDeposits)
            SendDepositsOverview(uid);
    }

    private void OnLoansQuery(EntityUid uid, FinanceLoansConsoleComponent _1, FinanceLoansQueryMessage _2)
    {
        SendLoans(uid);
    }

    private void OnDepositsOverviewQuery(EntityUid uid, FinanceLoansConsoleComponent _1, FinanceDepositsQueryMessage _2)
    {
        SendDepositsOverview(uid);
    }

    private void SendLoans(EntityUid console)
    {
        var rows = new List<FinanceLoanRow>();
        foreach (var snap in GetAllLoansSnapshot())
        {
            var name = ResolveCharacterName(snap.UserId); // Lua: prefer in-game character name for online users
            var seconds = -1; // Unknown per-user schedule
            var code = ResolveOnlineYupi(snap.UserId);
            rows.Add(new FinanceLoanRow(name, snap.Principal, seconds, string.IsNullOrWhiteSpace(code) ? string.Empty : code.ToUpperInvariant()));
        }
        _ui.SetUiState(console, NFFinanceLoansUiKey.Key, new FinanceLoansState(rows.ToArray()));
    }

    private void SendDepositsOverview(EntityUid console)
    {
        var rows = new List<FinanceDepositOverviewRow>();
        foreach (var snap in GetAllDepositsSnapshot())
        {
            var name = ResolveCharacterName(snap.UserId); // Lua: prefer in-game character name for online users
            var code = ResolveOnlineYupi(snap.UserId);
            var nextSec = (int)Math.Max(0, (snap.NextCapAt - _gameTiming.CurTime).TotalSeconds);
            var stopSec = (int)Math.Max(0, (snap.HardStopAt - _gameTiming.CurTime).TotalSeconds);
            rows.Add(new FinanceDepositOverviewRow(name, string.IsNullOrWhiteSpace(code) ? string.Empty : code.ToUpperInvariant(), snap.Id, snap.Principal, snap.Accrued, nextSec, stopSec, snap.RateModel));
        }
        _ui.SetUiState(console, NFFinanceLoansUiKey.Key, new FinanceDepositsState(rows.ToArray()));
    }

    #endregion

    private string ResolveOnlineYupi(NetUserId userId)
    {
        // Resolve current YUPI code for online users only. For offline users returns empty. //Lua
        foreach (var session in _players.Sessions)
        {
            if (session.UserId != userId)
                continue;
            if (session.AttachedEntity is not { Valid: true } ent)
                continue;
            if (!TryComp<BankAccountComponent>(ent, out var bank))
                continue;
            // Ensure code synchronously for the session owner //Lua
            var ensured = _bank.EnsureYupiForSessionSelected(session);
            return string.IsNullOrWhiteSpace(bank.YupiCode) ? ensured : bank.YupiCode;
        }
        return string.Empty;
    }

    private string ResolveCharacterName(NetUserId userId)
    {
        // Prefer the in-game entity name of the attached mob (character name) when online.
        foreach (var session in _players.Sessions)
        {
            if (session.UserId != userId)
                continue;
            if (session.AttachedEntity is { Valid: true } ent)
            {
                var inGameName = Name(ent);
                if (!string.IsNullOrWhiteSpace(inGameName))
                    return inGameName;
            }
            // Fallback to OOC session name if entity is unavailable
            return session.Name;
        }
        // Offline fallback: show raw userId if nothing else is available
        return userId.ToString();
    }

    private ICommonSession GetSession(EntityUid ent)
    {
        ICommonSession? s = null;
        if (_players.TryGetSessionByEntity(ent, out var ses))
            s = ses;
        return s!;
    }

    private NetUserId GetNetUser(EntityUid ent)
    {
        if (_players.TryGetSessionByEntity(ent, out var ses))
            return ses.UserId;
        return default;
    }
}


