using System.Linq;
using System.Threading.Tasks;
using Content.Server.Administration;
using Content.Server.Database;
using Content.Server.Preferences.Managers;
using Content.Server._NF.Bank;
using Content.Server.Chat.Managers; //Lua logs
using Content.Server.Administration.Logs; //Lua logs
using Content.Shared.Administration;
using Content.Shared.Database; //Lua logs
using Content.Shared.Preferences;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Chat;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._NF.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class BankCommand : IConsoleCommand
{
    [Dependency] private readonly IServerPreferencesManager _prefsManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IServerDbManager _dbManager = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!; //Lua logs
    [Dependency] private readonly IChatManager _chatManager = default!; //Lua logs

    public string Command => "bank";

    public string Description => Loc.GetString($"cmd-{Command}-desc"); // Lua Localization

    public string Help => Loc.GetString($"cmd-{Command}-help"); // Lua Localization

    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Loc.GetString("cmd-bank-wrong-args")); // Lua Localization
            return;
        }


        var target = args[0];

        if (!int.TryParse(args[1], out var amount))
        {
            shell.WriteError(Loc.GetString("cmd-bank-invalid-amount")); // Lua Localization
            return;
        }

        // First try online players by name
        var onlinePlayer = _playerManager.Sessions
            .FirstOrDefault(s => s.Name.Equals(target, StringComparison.OrdinalIgnoreCase));

        if (onlinePlayer != null)
        {
            // Handle online player
            await HandleOnlinePlayer(shell, onlinePlayer, amount, target);
            return;
        }

        // If player name not found online, try by ID
        if (Guid.TryParse(target, out var userId))
        {
            onlinePlayer = _playerManager.Sessions.FirstOrDefault(s => s.UserId == userId);
            if (onlinePlayer != null)
            {
                await HandleOnlinePlayer(shell, onlinePlayer, amount, target);
                return;
            }
        }

        // If not online, check cached preferences for offline players
        if (TryGetOfflinePlayerData(target, out var offlineUserId, out var offlinePrefs, out var offlineProfile))
        {
            await HandleOfflinePlayer(shell, offlineUserId, offlinePrefs, offlineProfile, amount, target);
            return;
        }

        // If not in cache, try the database
        var record = await _dbManager.GetPlayerRecordByUserName(target);
        if (record != null)
        {
            var dbUserId = record.UserId;
            var dbPrefs = await _dbManager.GetPlayerPreferencesAsync(dbUserId, default);
            if (dbPrefs != null &&
                dbPrefs.SelectedCharacterIndex >= 0 &&
                dbPrefs.Characters.TryGetValue(dbPrefs.SelectedCharacterIndex, out var dbProfile))
            {
                if (dbProfile is HumanoidCharacterProfile dbHumanoid)
                {
                    await HandleOfflinePlayer(shell, dbUserId, dbPrefs, dbHumanoid, amount, target);
                    return;
                }
            }
        }
        if (target == "@")
        {
            await HandleAllCharacters(shell, amount);
            return;
        }

        shell.WriteError(Loc.GetString("cmd-bank-player-not-found", ("player", target)));
    }

    private async Task HandleOnlinePlayer(IConsoleShell shell, ICommonSession targetSession, int amount, string target)
    {
        var bankSystem = _entitySystemManager.GetEntitySystem<BankSystem>();

        if (!_prefsManager.TryGetCachedPreferences(targetSession.UserId, out var prefs))
        {
            shell.WriteError(Loc.GetString("cmd-bank-preferences-not-found", ("player", target))); // Lua Localization
            return;
        }

        if (prefs.SelectedCharacter is not HumanoidCharacterProfile profile)
        {
            shell.WriteError(Loc.GetString("cmd-bank-invalid-character", ("player", target))); // Lua Localization
            return;
        }

        var currentBalance = profile.BankBalance;

        // Ensure the player won't have negative balance after withdrawal
        if (amount < 0 && Math.Abs(amount) > currentBalance)
        {
            shell.WriteError(Loc.GetString("cmd-bank-not-enough-money", ("player", target), ("balance", currentBalance), ("removeAmount", Math.Abs(amount)))); // Lua Localization
            return;
        }

        bool success;
        int? newBalance = null;

        // Check if player is currently in-game with an entity
        EntityUid? playerEntity = targetSession.AttachedEntity;

        if (playerEntity != null && _entityManager.HasComponent<BankAccountComponent>(playerEntity.Value))
        {
            var bankCompBefore = _entityManager.GetComponent<BankAccountComponent>(playerEntity.Value);
            var prevBalance = bankCompBefore.Balance;
            if (amount > 0)
            {
                success = bankSystem.TryBankDeposit(playerEntity.Value, amount);
                if (success) newBalance = _entityManager.GetComponent<BankAccountComponent>(playerEntity.Value).Balance;
            }
            else if (amount < 0)
            {
                success = bankSystem.TryBankWithdraw(playerEntity.Value, Math.Abs(amount));
                if (success) newBalance = _entityManager.GetComponent<BankAccountComponent>(playerEntity.Value).Balance;
            }
            else
            {
                shell.WriteLine(Loc.GetString("cmd-bank-no-change", ("player", target), ("balance", currentBalance))); // Lua Localization
                return;
            }
            currentBalance = prevBalance;
        }
        else
        {
            // Player is not in-game or entity has no bank account - update profile directly
            if (amount > 0)
            {
                success = bankSystem.TryBankDeposit(targetSession, prefs, profile, amount, out newBalance);
            }
            else if (amount < 0)
            {
                success = bankSystem.TryBankWithdraw(targetSession, prefs, profile, Math.Abs(amount), out newBalance);
            }
            else
            {
                shell.WriteLine(Loc.GetString("cmd-bank-no-change", ("player", target), ("balance", currentBalance))); // Lua Localization
                return;
            }
        }

        if (!success || newBalance == null)
        {
            shell.WriteError(Loc.GetString("cmd-bank-failed", ("player", target))); // Lua Localization
            return;
        }

        var finalBalance = newBalance.Value;
        shell.WriteLine(amount > 0 ? Loc.GetString("cmd-bank-added", ("amount", amount), ("player", target), ("balance", finalBalance)) : Loc.GetString("cmd-bank-removed", ("amount", Math.Abs(amount)), ("player", target), ("balance", finalBalance)));
        shell.WriteLine(Loc.GetString("cmd-bank-prev-current", ("prev", currentBalance), ("current", finalBalance)));
        var changeText = amount >= 0 ? $"+{Math.Abs(amount)}" : $"-{Math.Abs(amount)}";
        var notify = Loc.GetString("bank-program-change-balance-notification", ("balance", finalBalance), ("change", changeText), ("currencySymbol", "$"));
        _chatManager.ChatMessageToOne(ChatChannel.Notifications, notify, notify, EntityUid.Invalid, false, targetSession.Channel);

        //Lua start logs
        var executor = shell.Player?.Name ?? "CONSOLE";
        var absAmount = Math.Abs(amount);
        var actionText = amount > 0 ? Loc.GetString("cmd-bank-log-deposited") : Loc.GetString("cmd-bank-log-withdrew");

        _adminLogger.Add(LogType.BankTransaction, LogImpact.Extreme,
        $"{executor} {actionText} {absAmount} {(amount > 0 ? Loc.GetString("cmd-bank-admin-to") : Loc.GetString("cmd-bank-admin-from"))} {target}. {Loc.GetString("cmd-bank-admin-new-balance", ("balance", finalBalance))}");
    }

    private async Task HandleOfflinePlayer(IConsoleShell shell, NetUserId userId, PlayerPreferences prefs, HumanoidCharacterProfile profile, int amount, string target)
    {
        var bankSystem = _entitySystemManager.GetEntitySystem<BankSystem>();
        var currentBalance = profile.BankBalance;

        // Ensure the player won't have negative balance after withdrawal
        if (amount < 0 && Math.Abs(amount) > currentBalance)
        { shell.WriteError(Loc.GetString("cmd-bank-offline-not-enough", ("player", target), ("balance", currentBalance), ("removeAmount", Math.Abs(amount)))); return; }

        if (amount == 0)
        { shell.WriteLine(Loc.GetString("cmd-bank-offline-unchanged", ("player", target), ("balance", currentBalance))); return; }

        bool success;
        int? newBalance = null;

        // Use the new offline bank methods
        if (amount > 0)
        {
            success = await bankSystem.TryBankDepositOffline(userId, prefs, profile, amount);
            if (success)
                newBalance = currentBalance + amount;
        }
        else
        {
            success = await bankSystem.TryBankWithdrawOffline(userId, prefs, profile, Math.Abs(amount));
            if (success)
                newBalance = currentBalance - Math.Abs(amount);
        }

        if (!success || newBalance == null)
        { shell.WriteError(Loc.GetString("cmd-bank-offline-failed", ("player", target))); return; }
        shell.WriteLine(amount > 0 ? Loc.GetString("cmd-bank-offline-added", ("amount", amount), ("player", target), ("balance", newBalance.Value)) : Loc.GetString("cmd-bank-offline-removed", ("amount", Math.Abs(amount)), ("player", target), ("balance", newBalance.Value)));
    }

    private async Task HandleAllCharacters(IConsoleShell shell, int amount)
    {
        var bankSystem = _entitySystemManager.GetEntitySystem<BankSystem>();
        var processedUsers = new HashSet<NetUserId>();
        var processedCount = 0;
        var recipients = new List<string>();

        foreach (var session in _playerManager.Sessions)
        {
            if (!processedUsers.Add(session.UserId)) continue;
            var entity = session.AttachedEntity;
            if (entity == null) continue;
            if (!_entityManager.HasComponent<ActorComponent>(entity.Value)) continue;
            if (!_entityManager.HasComponent<BankAccountComponent>(entity.Value)) continue;
            var oldBalance = _entityManager.GetComponent<BankAccountComponent>(entity.Value).Balance;
            var success = false;
            if (amount > 0) success = bankSystem.TryBankDeposit(entity.Value, amount);
            else if (amount < 0) success = bankSystem.TryBankWithdraw(entity.Value, Math.Abs(amount));
            else continue;
            if (success)
            {
                processedCount++;
                var displayName = session.Name;
                if (_prefsManager.TryGetCachedPreferences(session.UserId, out var prefs) && prefs.SelectedCharacter is HumanoidCharacterProfile humanoidProfile)
                { displayName = humanoidProfile.Name; }
                var newBalance = _entityManager.GetComponent<BankAccountComponent>(entity.Value).Balance;
                recipients.Add(Loc.GetString("cmd-bank-batch-recipient", ("name", displayName), ("prev", oldBalance), ("next", newBalance)));
                var changeText = amount >= 0 ? $"+{Math.Abs(amount)}" : $"-{Math.Abs(amount)}";
                var notify = Loc.GetString("bank-program-change-balance-notification", ("balance", newBalance), ("change", changeText), ("currencySymbol", "$"));
                _chatManager.ChatMessageToOne(ChatChannel.Notifications, notify, notify, EntityUid.Invalid, false, session.Channel);
            }
        }

        if (processedCount > 0) shell.WriteLine(Loc.GetString("cmd-bank-batch-summary", ("amount", amount), ("count", processedCount), ("recipients", string.Join(", ", recipients))));
        else shell.WriteLine(Loc.GetString("cmd-bank-batch-none"));
    }

    private bool TryGetOfflinePlayerData(string username, out NetUserId userId, out PlayerPreferences prefs, out HumanoidCharacterProfile profile)
    {
        userId = default;
        prefs = null!;
        profile = null!;

        // Check all users in the preferences cache
        foreach (var playerData in _playerManager.GetAllPlayerData())
        {
            if (_prefsManager.TryGetCachedPreferences(playerData.UserId, out var cachedPrefs))
            {
                foreach (var (_, characterProfile) in cachedPrefs.Characters)
                {
                    if (characterProfile is HumanoidCharacterProfile humanoid &&
                        humanoid.Name.Equals(username, StringComparison.OrdinalIgnoreCase))
                    {
                        userId = playerData.UserId;
                        prefs = cachedPrefs;
                        profile = humanoid;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var options = new List<string>();

            // Add online players
            options.AddRange(_playerManager.Sessions.Select(s => s.Name));

            // Add players from cached preferences
            foreach (var playerData in _playerManager.GetAllPlayerData())
            {
                if (_prefsManager.TryGetCachedPreferences(playerData.UserId, out var prefs))
                {
                    foreach (var (_, profile) in prefs.Characters)
                    {
                        if (profile is HumanoidCharacterProfile humanoid)
                        {
                            options.Add(humanoid.Name);
                        }
                    }
                }
            }
            return CompletionResult.FromHintOptions(options.Distinct(), Loc.GetString("cmd-bank-completion-arg-hint"));
        }

        if (args.Length == 2)
        {
            // For the amount parameter, provide some common values as suggestions
            var amountOptions = new List<CompletionOption>
            {
                new CompletionOption("100", Loc.GetString("cmd-bank-hint-add", ("amount", 100))), // Lua Localization
                new CompletionOption("1000", Loc.GetString("cmd-bank-hint-add", ("amount", 1000))), // Lua Localization
                new CompletionOption("10000", Loc.GetString("cmd-bank-hint-add", ("amount", 10000))), // Lua Localization
                new CompletionOption("-100", Loc.GetString("cmd-bank-hint-remove", ("amount", 100))), // Lua Localization
                new CompletionOption("-1000", Loc.GetString("cmd-bank-hint-remove", ("amount", 1000))) // Lua Localization
            };

            return CompletionResult.FromOptions(amountOptions);
        }

        return CompletionResult.Empty;
    }
}
