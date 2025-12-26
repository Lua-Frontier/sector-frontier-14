/*
 * LuaWorld - This file is licensed under AGPLv3
 * Copyright (c) 2025 LuaWorld Contributors
 * See AGPLv3.txt for details.
 */
using Content.Shared._NF.Bank;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Preferences;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Configuration;
using Robust.Server.Containers;
using Robust.Shared.Random;

namespace Content.Server._NF.Bank;

public sealed partial class BankSystem : SharedBankSystem
{
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly char[] YupiLetters = "ABCDEFGHJKLMNPQRSTUVWXYZ".ToCharArray();
    private static readonly char[] YupiDigits = "123456789".ToCharArray();

    private bool IsValidYupiCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != 6) return false;
        for (int i = 0; i < code.Length; i++)
        {
            var ch = char.ToUpperInvariant(code[i]);
            if (!(Array.IndexOf(YupiLetters, ch) >= 0 || Array.IndexOf(YupiDigits, ch) >= 0)) return false;
        }
        return true;
    }

    private string GenerateYupiCandidate()
    {
        Span<char> buf = stackalloc char[6];
        for (int i = 0; i < 6; i++)
        {
            var pickDigit = _random.Next(0, 2) == 0;
            if (pickDigit) buf[i] = YupiDigits[_random.Next(YupiDigits.Length)];
            else buf[i] = YupiLetters[_random.Next(YupiLetters.Length)];
        }
        return new string(buf);
    }

    private string GenerateUniqueYupiCode()
    {
        var comparer = StringComparer.OrdinalIgnoreCase;
        var existing = new HashSet<string>(comparer);
        foreach (var comp in EntityQuery<BankAccountComponent>(true))
        { if (!string.IsNullOrEmpty(comp.YupiCode)) existing.Add(comp.YupiCode); }
        for (int attempt = 0; attempt < 1000; attempt++)
        {
            var candidate = GenerateYupiCandidate();
            if (!existing.Contains(candidate)) return candidate.ToUpperInvariant();
        }
        return $"YU{DateTime.UtcNow.Ticks % 1000000:D6}";
    }

    public string EnsureYupiForSessionSelected(ICommonSession session)
    {
        try
        {
            if (session.AttachedEntity is not { Valid: true } ent) return string.Empty;
            if (!TryComp<BankAccountComponent>(ent, out var bank)) return string.Empty;
            if (IsValidYupiCode(bank.YupiCode)) return bank.YupiCode;
            var code = GenerateUniqueYupiCode();
            bank.YupiCode = code;
            Dirty(ent, bank);
            return code;
        }
        catch (Exception e) { _log.Warning($"EnsureYupiForSessionSelected failed: {e.Message}"); return string.Empty; }
    }

    public string EnsureYupiForEntity(EntityUid ent)
    {
        if (!TryComp<BankAccountComponent>(ent, out var bank)) return string.Empty;
        if (IsValidYupiCode(bank.YupiCode)) return bank.YupiCode;
        var code = GenerateUniqueYupiCode();
        bank.YupiCode = code;
        Dirty(ent, bank);
        return code;
    }

    private void EnsureYupiForAllUsers()
    {
        try
        {
            _log.Info("YUPI migration: ensuring codes for all bank accounts...");
            var enumerator = EntityQueryEnumerator<BankAccountComponent>();
            while (enumerator.MoveNext(out var entityUid, out var bank))
            {
                try
                {
                    if (IsValidYupiCode(bank.YupiCode)) continue;
                    var code = GenerateUniqueYupiCode();
                    bank.YupiCode = code;
                    Dirty(entityUid, bank);
                }
                catch (Exception ex) { _log.Warning($"YUPI migration: failed for entity {entityUid}: {ex.Message}"); }
            }
            _log.Info("YUPI migration: done.");
        }
        catch (Exception e) { _log.Error($"YUPI migration failed: {e}"); }
    }

    public bool TryResolveOnlineByYupiCode(string inputCode, out EntityUid target)
    {
        target = default;
        if (string.IsNullOrWhiteSpace(inputCode) || inputCode.Length != 6) return false;
        var norm = inputCode.ToUpperInvariant();
        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } ent) continue;
            if (!TryComp<BankAccountComponent>(ent, out var bank)) continue;
            if (string.Equals(bank.YupiCode, norm, StringComparison.OrdinalIgnoreCase))
            { target = ent; return true; }
        }
        return false;
    }

    private readonly Dictionary<NetUserId, Queue<(DateTime Time, int Amount)>> _yupiHistoryByUser = new();

    private int GetWindowSum(NetUserId userId, DateTime now)
    {
        if (!_yupiHistoryByUser.TryGetValue(userId, out var queue)) return 0;
        while (queue.Count > 0 && (now - queue.Peek().Time) >= TimeSpan.FromMinutes(30)) queue.Dequeue();
        var sum = 0;
        foreach (var e in queue) sum += e.Amount;
        return sum;
    }

    public enum YupiTransferError
    {
        None,
        InvalidTarget,
        SelfTransfer,
        InvalidAmount,
        ExceedsPerTransferLimit,
        InsufficientFunds,
        ExceedsWindowLimit
    }

    public bool TryYupiTransfer(EntityUid sender, string targetCodeInput, int amount, out YupiTransferError error, out int newSenderBalance, out int receiverAmount, out string? receiverCode)
    {
        error = YupiTransferError.None;
        newSenderBalance = 0;
        receiverAmount = 0;
        receiverCode = null;
        if (amount <= 0)
        { error = YupiTransferError.InvalidAmount; return false; }
        if (!CheckTransferLimit(amount))
        { error = YupiTransferError.ExceedsPerTransferLimit; return false; }
        var source = GetRootOwner(sender);
        if (!TryComp<BankAccountComponent>(source, out _))
        { error = YupiTransferError.InvalidAmount; return false; }
        if (!_playerManager.TryGetSessionByEntity(source, out var senderSession) || !_prefsManager.TryGetCachedPreferences(senderSession.UserId, out var senderPrefs) || senderPrefs.SelectedCharacter is not HumanoidCharacterProfile senderProfile)
        { error = YupiTransferError.InvalidAmount; return false; }
        if (!TryResolveOnlineByYupiCode(targetCodeInput, out var target))
        { error = YupiTransferError.InvalidTarget; return false; }
        if (target == source) { error = YupiTransferError.SelfTransfer; return false; }
        var now = DateTime.UtcNow;
        Queue<(DateTime Time, int Amount)>? transferHistoryQueue;
        var sumInWindow = GetWindowSum(senderSession.UserId, now);
        int commissionPercent;
        if (sumInWindow >= 100_000) commissionPercent = 13;
        else if (sumInWindow + amount <= 100_000) commissionPercent = 3;
        else
        {
            var partLow = 100_000 - sumInWindow;
            var partHigh = amount - partLow;
            var comm = (int)Math.Ceiling(partLow * 0.03) + (int)Math.Ceiling(partHigh * 0.13);
            var totalCharge = amount + comm;
            if (senderProfile.BankBalance < totalCharge)
            { error = YupiTransferError.InsufficientFunds; return false; }
            if (!TryBankWithdraw(source, totalCharge))
            { error = YupiTransferError.InvalidAmount; return false; }
            if (!TryBankDeposit(target, amount))
            { TryBankDeposit(source, totalCharge); error = YupiTransferError.InvalidTarget; return false; }
            if (!_yupiHistoryByUser.TryGetValue(senderSession.UserId, out transferHistoryQueue)) transferHistoryQueue = _yupiHistoryByUser[senderSession.UserId] = new();
            transferHistoryQueue.Enqueue((now, amount));
            TryGetBalance(source, out newSenderBalance);
            receiverAmount = amount;
            if (TryComp<BankAccountComponent>(target, out var tBank))  receiverCode = tBank.YupiCode;
            return true;
        }
        var commission = (int)Math.Ceiling(amount * (commissionPercent / 100.0));
        var total = amount + commission;
        if (senderProfile.BankBalance < total)
        { error = YupiTransferError.InsufficientFunds; return false; }
        if (!TryBankWithdraw(source, total))
        { error = YupiTransferError.InvalidAmount; return false; }
        if (!TryBankDeposit(target, amount))
        { TryBankDeposit(source, total); error = YupiTransferError.InvalidTarget; return false; }
        if (!_yupiHistoryByUser.TryGetValue(senderSession.UserId, out transferHistoryQueue)) transferHistoryQueue = _yupiHistoryByUser[senderSession.UserId] = new();
        transferHistoryQueue!.Enqueue((now, amount));
        TryGetBalance(source, out newSenderBalance);
        receiverAmount = amount;
        if (TryComp<BankAccountComponent>(target, out var tBank2)) receiverCode = tBank2.YupiCode;
        return true;
    }

    private EntityUid GetRootOwner(EntityUid ent)
    {
        var current = ent;
        while (_container.TryGetContainingContainer(current, out var cont))
        { current = cont.Owner; }
        return current;
    }
}
