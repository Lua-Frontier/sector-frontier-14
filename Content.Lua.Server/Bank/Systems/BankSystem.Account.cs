// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.GameTicking;
using Content.Server.Preferences.Managers;
using Content.Lua.Shared.Bank; // SectorBankComponent
using Content.Lua.Shared.Bank;
using Content.Shared._NF.Bank;
using Content.Shared._NF.Bank.Events;
using Content.Shared.GameTicking;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Lua.Server.Bank;

public sealed partial class BankSystem : SharedBankSystem, IBankSystem
{
    [Dependency] private readonly IServerPreferencesManager _prefsManager = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;

    private ISawmill _log = default!;

    private readonly Dictionary<NetUserId, List<BankAccountOperation>> _operationHistoryByUser = new();

    public override void Initialize()
    {
        base.Initialize();
        _log = Logger.GetSawmill("bank");
        InitializeATM();
        InitializeStationATM();

        SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnPlayerLobbyJoin);
        SubscribeLocalEvent<SectorBankComponent, ComponentInit>(OnSectorInit);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateSectorBanks(frameTime);
    }

    public void OnCleanup(RoundRestartCleanupEvent _)
    {
        CleanupLedger();
        _operationHistoryByUser.Clear();
        ClearGalBankCodeCache();
    }

    private void AddOperationRecord(NetUserId userId, BankAccountOperationType type, int value)
    {
        if (!_operationHistoryByUser.TryGetValue(userId, out var history))
        {
            history = new List<BankAccountOperation>();
            _operationHistoryByUser[userId] = history;
        }

        history.Add(new BankAccountOperation(type, value, _timing.CurTime));
        if (history.Count > 50)
            history.RemoveRange(0, 30);
    }

    public bool TryGetOperationHistory(NetUserId userId, out IReadOnlyList<BankAccountOperation> history)
    {
        if (_operationHistoryByUser.TryGetValue(userId, out var list))
        {
            history = list;
            return true;
        }

        history = Array.Empty<BankAccountOperation>();
        return false;
    }

    public bool TryBankWithdraw(EntityUid mobUid, int amount)
    {
        if (!_playerManager.TryGetSessionByEntity(mobUid, out var session))
        {
            _log.Info($"TryBankWithdraw: {mobUid} has no attached session");
            return false;
        }

        return TryBankWithdraw(session, amount, out _);
    }

    public bool TryBankDeposit(EntityUid mobUid, int amount)
    {
        if (!_playerManager.TryGetSessionByEntity(mobUid, out var session))
        {
            _log.Info($"TryBankDeposit: {mobUid} has no attached session");
            return false;
        }

        return TryBankDeposit(session, amount, out _);
    }

    public bool TryBankWithdraw(ICommonSession session, int amount, [NotNullWhen(true)] out int? newBalance)
    {
        newBalance = null;
        if (amount <= 0)
        {
            _log.Info($"TryBankWithdraw: {amount} is invalid");
            return false;
        }

        if (!_prefsManager.TryGetCachedPreferences(session.UserId, out var prefs))
        {
            _log.Info($"TryBankWithdraw: {session.UserId} has no cached prefs");
            return false;
        }

        if (prefs.BankBalance < amount)
        {
            _log.Info($"TryBankWithdraw: {session.UserId} tried to withdraw {amount}, but has insufficient funds ({prefs.BankBalance})");
            return false;
        }

        var balance = prefs.BankBalance - amount;
        if (!_prefsManager.TryApplyBankDelta(session.UserId, -amount, out balance))
            return false;

        newBalance = balance;
        AddOperationRecord(session.UserId, BankAccountOperationType.Withdraw, amount);
        RaiseLocalEvent(new BalanceChangedEvent(session, balance));
        _log.Info($"{session.UserId} withdrew {amount}");
        return true;
    }

    public bool TryBankDeposit(ICommonSession session, int amount, [NotNullWhen(true)] out int? newBalance)
    {
        newBalance = null;
        if (amount <= 0)
        {
            _log.Info($"TryBankDeposit: {amount} is invalid");
            return false;
        }

        if (!_prefsManager.TryGetCachedPreferences(session.UserId, out var prefs))
        {
            _log.Info($"TryBankDeposit: {session.UserId} has no cached prefs");
            return false;
        }

        if (prefs.BankBalance > int.MaxValue - amount)
        {
            _log.Info($"TryBankDeposit: {session.UserId} deposit {amount} would overflow ({prefs.BankBalance})");
            return false;
        }

        var balance = prefs.BankBalance + amount;
        if (!_prefsManager.TryApplyBankDelta(session.UserId, amount, out balance))
            return false;

        newBalance = balance;
        AddOperationRecord(session.UserId, BankAccountOperationType.Deposit, amount);
        RaiseLocalEvent(new BalanceChangedEvent(session, balance));
        _log.Info($"{session.UserId} deposited {amount}");
        return true;
    }

    public async Task<bool> TryBankWithdrawOffline(NetUserId userId, int amount)
    {
        if (amount <= 0)
        {
            _log.Info($"TryBankWithdrawOffline: {amount} is invalid");
            return false;
        }

        var (success, newBalance) = await _prefsManager.ApplyBankDeltaAsync(userId, -amount);
        if (!success)
        {
            _log.Info($"TryBankWithdrawOffline: {userId} withdraw {amount} failed (missing prefs or insufficient funds)");
            return false;
        }

        AddOperationRecord(userId, BankAccountOperationType.Withdraw, amount);
        if (_playerManager.TryGetSessionById(userId, out var session))
            RaiseLocalEvent(new BalanceChangedEvent(session, newBalance));
        _log.Info($"Offline player {userId} withdrew {amount}");
        return true;
    }

    public async Task<bool> TryBankDepositOffline(NetUserId userId, int amount)
    {
        if (amount <= 0)
        {
            _log.Info($"TryBankDepositOffline: {amount} is invalid");
            return false;
        }

        var (success, newBalance) = await _prefsManager.ApplyBankDeltaAsync(userId, amount);
        if (!success)
        {
            _log.Info($"TryBankDepositOffline: {userId} deposit {amount} failed (missing prefs or overflow)");
            return false;
        }

        AddOperationRecord(userId, BankAccountOperationType.Deposit, amount);
        if (_playerManager.TryGetSessionById(userId, out var session))
            RaiseLocalEvent(new BalanceChangedEvent(session, newBalance));
        _log.Info($"Offline player {userId} deposited {amount}");
        return true;
    }

    public bool TryGetBalance(EntityUid ent, out int balance)
    {
        if (!_playerManager.TryGetSessionByEntity(ent, out var session))
        {
            _log.Info($"{ent} has no attached session");
            balance = 0;
            return false;
        }

        return TryGetBalance(session, out balance);
    }

    public bool TryGetBalance(ICommonSession session, out int balance)
    {
        if (!_prefsManager.TryGetCachedPreferences(session.UserId, out var prefs))
        {
            _log.Info($"{session.UserId} has no cached prefs");
            balance = 0;
            return false;
        }

        balance = prefs.BankBalance;
        return true;
    }

    public bool TryGetBalance(NetUserId userId, out int balance)
    {
        if (!_prefsManager.TryGetCachedPreferences(userId, out var prefs))
        {
            balance = 0;
            return false;
        }

        balance = prefs.BankBalance;
        return true;
    }

    public bool HasAccountBank(EntityUid ent)
    {
        return TryGetBalance(ent, out _);
    }

    private void OnPlayerLobbyJoin(PlayerJoinedLobbyEvent args)
    {
        var cts = new CancellationToken();
        _prefsManager.RefreshPreferencesAsync(args.PlayerSession, cts);
        InvalidateGalBankCode(args.PlayerSession.UserId);
        EnsureGalBankForUser(args.PlayerSession.UserId);
    }
}
