using Content.Shared._NF.Bank;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Preferences;
using Robust.Shared.Network;
using Robust.Shared.Player;
using System.Threading.Tasks;
using Robust.Shared.Configuration;
using Robust.Server.Containers;

namespace Content.Server._NF.Bank;

public sealed partial class BankSystem : SharedBankSystem
{
    [Dependency] private readonly ContainerSystem _container = default!; // Lua
    [Dependency] private readonly IConfigurationManager _cfg = default!; // Lua

    private static readonly char[] YupiLetters = "ABCDEFGHJKLMNPQRSTUVWXYZ".ToCharArray(); // Exclude I, O
    private static readonly char[] YupiDigits = "123456789".ToCharArray(); // Exclude 0

    private bool IsValidYupiCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != 6)
            return false;
        for (int i = 0; i < code.Length; i++)
        {
            var ch = char.ToUpperInvariant(code[i]);
            if (!(Array.IndexOf(YupiLetters, ch) >= 0 || Array.IndexOf(YupiDigits, ch) >= 0))
                return false;
        }
        return true;
    }

    private string GenerateYupiCandidate()
    {
        var rand = new Random();
        Span<char> buf = stackalloc char[6];
        for (int i = 0; i < 6; i++)
        {
            // mix letters and digits roughly evenly
            var pickDigit = rand.Next(0, 2) == 0;
            if (pickDigit)
                buf[i] = YupiDigits[rand.Next(YupiDigits.Length)];
            else
                buf[i] = YupiLetters[rand.Next(YupiLetters.Length)];
        }
        return new string(buf);
    }

    private async Task<string> GenerateUniqueYupiCodeAsync()
    {
        // Build a HashSet of all existing codes (case-insensitive) from cached prefs and DB.
        var comparer = StringComparer.OrdinalIgnoreCase;
        var existing = new HashSet<string>(comparer);

        foreach (var sessionData in _playerManager.GetAllPlayerData())
        {
            try
            {
                if (_prefsManager.TryGetCachedPreferences(sessionData.UserId, out var cached))
                {
                    foreach (var (_, prof) in cached.Characters)
                    {
                        if (prof is HumanoidCharacterProfile human && !string.IsNullOrEmpty(human.YupiAccountCode))
                            existing.Add(human.YupiAccountCode);
                    }
                }
                else
                {
                    var prefs = await _db.GetPlayerPreferencesAsync(sessionData.UserId, default);
                    if (prefs != null)
                    {
                        foreach (var (_, prof) in prefs.Characters)
                        {
                            if (prof is HumanoidCharacterProfile human && !string.IsNullOrEmpty(human.YupiAccountCode))
                                existing.Add(human.YupiAccountCode);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _log.Warning($"Could not read preferences for {sessionData.UserId}: {e.Message}");
            }
        }

        // Generate until unique
        for (int attempt = 0; attempt < 1000; attempt++)
        {
            var candidate = GenerateYupiCandidate();
            if (!existing.Contains(candidate))
                return candidate.ToUpperInvariant();
        }

        // Fallback (shouldn't happen)
        return $"YU{DateTime.UtcNow.Ticks % 1000000:D6}";
    }

    /// <summary>
    /// Ensures that the selected character of the given session has a valid YUPI code.
    /// Returns the code (existing or newly generated), or empty string if unavailable.
    /// </summary>
    public async Task<string> EnsureYupiForSessionSelected(ICommonSession session)
    {
        try
        {
            if (!_prefsManager.TryGetCachedPreferences(session.UserId, out var prefs))
                return string.Empty;
            if (prefs.SelectedCharacter is not HumanoidCharacterProfile profile)
                return string.Empty;
            if (IsValidYupiCode(profile.YupiAccountCode))
                return profile.YupiAccountCode;

            var index = prefs.IndexOfCharacter(profile);
            if (index == -1)
                return string.Empty;

            var code = await GenerateUniqueYupiCodeAsync();
            var newProfile = profile.WithYupiAccountCode(code);
            await _prefsManager.SetProfile(session.UserId, index, newProfile, validateFields: false);
            return code;
        }
        catch (Exception e)
        {
            _log.Warning($"EnsureYupiForSessionSelected failed: {e.Message}");
            return string.Empty;
        }
    }

    private async Task EnsureYupiForAllUsersAsync()
    {
        try
        {
            _log.Info("YUPI migration: ensuring codes for all character slots...");
            foreach (var pdata in _playerManager.GetAllPlayerData())
            {
                try
                {
                    PlayerPreferences? prefs;
                    if (_prefsManager.TryGetCachedPreferences(pdata.UserId, out var cached))
                        prefs = cached;
                    else
                        prefs = await _db.GetPlayerPreferencesAsync(pdata.UserId, default);

                    if (prefs == null)
                        continue;

                    var assigned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var (_, profBase) in prefs.Characters)
                    {
                        if (profBase is HumanoidCharacterProfile hp && !string.IsNullOrEmpty(hp.YupiAccountCode))
                            assigned.Add(hp.YupiAccountCode);
                    }

                    var anyChanged = false;
                    foreach (var (idx, profBase) in prefs.Characters)
                    {
                        if (profBase is not HumanoidCharacterProfile hp)
                            continue;
                        if (IsValidYupiCode(hp.YupiAccountCode))
                            continue;

                        var code = await GenerateUniqueYupiCodeAsync();
                        while (assigned.Contains(code))
                            code = await GenerateUniqueYupiCodeAsync();
                        assigned.Add(code);
                        var newProf = hp.WithYupiAccountCode(code);

                        if (_prefsManager.TryGetCachedPreferences(pdata.UserId, out _))
                            await _prefsManager.SetProfile(pdata.UserId, idx, newProf, validateFields: false);
                        else
                            await _db.SaveCharacterSlotAsync(pdata.UserId, newProf, idx);

                        anyChanged = true;
                    }

                    if (anyChanged)
                        _log.Info($"YUPI migration: assigned codes for user {pdata.UserId}");
                }
                catch (Exception ex)
                {
                    _log.Warning($"YUPI migration: failed for user {pdata.UserId}: {ex.Message}");
                }
            }
            _log.Info("YUPI migration: done.");
        }
        catch (Exception e)
        {
            _log.Error($"YUPI migration failed: {e}");
        }
    }

    public bool TryResolveOnlineByYupiCode(string inputCode, out EntityUid target, out HumanoidCharacterProfile? profile)
    {
        target = default;
        profile = null;
        if (string.IsNullOrWhiteSpace(inputCode) || inputCode.Length != 6)
            return false;
        var norm = inputCode.ToUpperInvariant();
        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } ent)
                continue;
            if (!_prefsManager.TryGetCachedPreferences(session.UserId, out var prefs))
                continue;
            if (prefs.SelectedCharacter is not HumanoidCharacterProfile prof)
                continue;
            if (string.Equals(prof.YupiAccountCode, norm, StringComparison.OrdinalIgnoreCase))
            {
                target = ent;
                profile = prof;
                return true;
            }
        }
        return false;
    }

    // Sliding 30-minute window history per user for YUPI transfers
    private readonly Dictionary<NetUserId, Queue<(DateTime Time, int Amount)>> _yupiHistoryByUser = new();

    private int GetWindowSum(NetUserId userId, DateTime now)
    {
        if (!_yupiHistoryByUser.TryGetValue(userId, out var q))
            return 0;
        while (q.Count > 0 && (now - q.Peek().Time) >= TimeSpan.FromMinutes(30))
            q.Dequeue();
        var sum = 0;
        foreach (var e in q)
            sum += e.Amount;
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

    public bool TryYupiTransfer(EntityUid sender, string targetCodeInput, int amount,
        out YupiTransferError error, out int newSenderBalance, out int receiverAmount, out string? receiverCode)
    {
        error = YupiTransferError.None;
        newSenderBalance = 0;
        receiverAmount = 0;
        receiverCode = null;

        if (amount <= 0)
        {
            error = YupiTransferError.InvalidAmount;
            return false;
        }

        // Lua: проверка лимита перевода из CVar
        if (!CheckTransferLimit(amount))
        {
            error = YupiTransferError.ExceedsPerTransferLimit;
            return false;
        }

        // Use the real owner of the device as the source of funds
        var source = GetRootOwner(sender);
        if (!TryComp<BankAccountComponent>(source, out _))
        {
            error = YupiTransferError.InvalidAmount;
            return false;
        }

        if (!_playerManager.TryGetSessionByEntity(source, out var senderSession) ||
            !_prefsManager.TryGetCachedPreferences(senderSession.UserId, out var senderPrefs) ||
            senderPrefs.SelectedCharacter is not HumanoidCharacterProfile senderProfile)
        {
            error = YupiTransferError.InvalidAmount;
            return false;
        }

        if (!TryResolveOnlineByYupiCode(targetCodeInput, out var target, out var targetProfile))
        {
            error = YupiTransferError.InvalidTarget;
            return false;
        }

        if (target == source)
        {
            error = YupiTransferError.SelfTransfer;
            return false;
        }

        // Commission logic with true sliding 30-minute window
        var now = DateTime.UtcNow;
        var sumInWindow = GetWindowSum(senderSession.UserId, now);

        int commissionPercent;
        if (sumInWindow >= 100_000)
            commissionPercent = 13;
        else if (sumInWindow + amount <= 100_000)
            commissionPercent = 3;
        else
        {
            var partLow = 100_000 - sumInWindow;
            var partHigh = amount - partLow;
            var comm = (int)Math.Ceiling(partLow * 0.03) + (int)Math.Ceiling(partHigh * 0.13);
            var totalCharge = amount + comm;
            if (senderProfile.BankBalance < totalCharge)
            {
                error = YupiTransferError.InsufficientFunds;
                return false;
            }

            if (!TryBankWithdraw(source, totalCharge))
            {
                error = YupiTransferError.InvalidAmount;
                return false;
            }
            if (!TryBankDeposit(target, amount))
            {
                TryBankDeposit(source, totalCharge);
                error = YupiTransferError.InvalidTarget;
                return false;
            }

            if (!_yupiHistoryByUser.TryGetValue(senderSession.UserId, out var q))
                _yupiHistoryByUser[senderSession.UserId] = q = new();
            q.Enqueue((now, amount));

            TryGetBalance(source, out newSenderBalance);
            receiverAmount = amount;
            receiverCode = targetProfile?.YupiAccountCode ?? "";
            return true;
        }

        var commission = (int)Math.Ceiling(amount * (commissionPercent / 100.0));
        var total = amount + commission;
        if (senderProfile.BankBalance < total)
        {
            error = YupiTransferError.InsufficientFunds;
            return false;
        }
        if (!TryBankWithdraw(source, total))
        {
            error = YupiTransferError.InvalidAmount;
            return false;
        }
        if (!TryBankDeposit(target, amount))
        {
            TryBankDeposit(source, total);
            error = YupiTransferError.InvalidTarget;
            return false;
        }

        if (!_yupiHistoryByUser.TryGetValue(senderSession.UserId, out var q2))
            _yupiHistoryByUser[senderSession.UserId] = q2 = new();
        q2.Enqueue((now, amount));

        TryGetBalance(source, out newSenderBalance);
        receiverAmount = amount;
        receiverCode = targetProfile?.YupiAccountCode ?? "";
        return true;
    }

    private EntityUid GetRootOwner(EntityUid ent)
    {
        var current = ent;
        while (_container.TryGetContainingContainer(current, out var cont))
        {
            current = cont.Owner;
        }
        return current;
    }
}
