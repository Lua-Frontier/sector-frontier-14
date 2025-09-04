using System;
using System.Collections.Generic;
using System.Threading;
using Content.Server.Database;
using Content.Server.Preferences.Managers;
using Content.Server.GameTicking;
using Content.Shared._NF.Bank;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Preferences;
using Robust.Shared.Player;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Content.Shared._NF.Bank.Events;
using Content.Shared._NF.Finance;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.GameStates;
using Content.Shared.GameTicking;
using Robust.Shared.Network;
using Robust.Server.Containers;

namespace Content.Server._NF.Bank;

public sealed partial class BankSystem : SharedBankSystem
{
	[Dependency] private readonly IConfigurationManager _cfg = default!;
	[Dependency] private readonly IServerPreferencesManager _prefsManager = default!;
	[Dependency] private readonly ISharedPlayerManager _playerManager = default!;
	[Dependency] private readonly IServerDbManager _db = default!;
	[Dependency] private readonly ContainerSystem _container = default!;

	private ISawmill _log = default!;

	public override void Initialize()
	{
		base.Initialize();
		_log = Logger.GetSawmill("bank");
		InitializeATM();
		InitializeStationATM();

		SubscribeLocalEvent<BankAccountComponent, PreferencesLoadedEvent>(OnPreferencesLoaded); // For late-add bank accounts
		SubscribeLocalEvent<BankAccountComponent, ComponentInit>(OnInit); // For late-add bank accounts
		SubscribeLocalEvent<BankAccountComponent, PlayerAttachedEvent>(OnPlayerAttached);
		SubscribeLocalEvent<BankAccountComponent, PlayerDetachedEvent>(OnPlayerDetached);
		SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnPlayerLobbyJoin);
		SubscribeLocalEvent<SectorBankComponent, ComponentInit>(OnSectorInit);

		SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);

		// Ensure migration: assign YUPI codes to all existing characters (once on boot).
		_ = EnsureYupiForAllUsersAsync();
	}

	public override void Update(float frameTime)
	{
		base.Update(frameTime);
		UpdateSectorBanks(frameTime);
	}

	public void OnCleanup(RoundRestartCleanupEvent _)
	{
		CleanupLedger();
	}

	/// <summary>
	/// Attempts to remove money from a character's bank account.
	/// This should always be used instead of attempting to modify the BankAccountComponent directly.
	/// When successful, the entity's BankAccountComponent will be updated with their current balance.
	/// </summary>
	/// <param name="mobUid">The UID that the bank account is attached to, typically the player controlled mob</param>
	/// <param name="amount">The integer amount of which to decrease the bank account</param>
	/// <returns>true if the transaction was successful, false if it was not</returns>
	public bool TryBankWithdraw(EntityUid mobUid, int amount)
	{
		if (amount <= 0)
		{
			_log.Info($"TryBankWithdraw: {amount} is invalid");
			return false;
		}

		if (!TryComp<BankAccountComponent>(mobUid, out var bank))
		{
			_log.Info($"TryBankWithdraw: {mobUid} has no bank account");
			return false;
		}

		if (!_playerManager.TryGetSessionByEntity(mobUid, out var session))
		{
			_log.Info($"TryBankWithdraw: {mobUid} has no attached session");
			return false;
		}

		if (!_prefsManager.TryGetCachedPreferences(session.UserId, out var prefs))
		{
			_log.Info($"TryBankWithdraw: {mobUid} has no cached prefs");
			return false;
		}

		if (prefs.SelectedCharacter is not HumanoidCharacterProfile profile)
		{
			_log.Info($"TryBankWithdraw: {mobUid} has the wrong prefs type");
			return false;
		}

		if (TryBankWithdraw(session, prefs, profile, amount, out var newBalance))
		{
			bank.Balance = newBalance.Value;
			Dirty(mobUid, bank);
			_log.Info($"{mobUid} withdrew {amount}");
			return true;
		}
		return false;
	}

	/// <summary>
	/// Attempts to add money to a character's bank account. This should always be used instead of attempting to modify the bankaccountcomponent directly
	/// </summary>
	/// <param name="mobUid">The UID that the bank account is connected to, typically the player controlled mob</param>
	/// <param name="amount">The amount of spesos to remove from the bank account</param>
	/// <returns>true if the transaction was successful, false if it was not</returns>
	public bool TryBankDeposit(EntityUid mobUid, int amount)
	{
		if (amount <= 0)
		{
			_log.Info($"TryBankDeposit: {amount} is invalid");
			return false;
		}

		if (!TryComp<BankAccountComponent>(mobUid, out var bank))
		{
			_log.Info($"TryBankDeposit: {mobUid} has no bank account");
			return false;
		}

		if (!_playerManager.TryGetSessionByEntity(mobUid, out var session))
		{
			_log.Info($"TryBankDeposit: {mobUid} has no attached session");
			return false;
		}

		if (!_prefsManager.TryGetCachedPreferences(session.UserId, out var prefs))
		{
			_log.Info($"TryBankDeposit: {mobUid} has no cached prefs");
			return false;
		}

		if (prefs.SelectedCharacter is not HumanoidCharacterProfile profile)
		{
			_log.Info($"TryBankDeposit: {mobUid} has the wrong prefs type");
			return false;
		}

		if (TryBankDeposit(session, prefs, profile, amount, out var newBalance))
		{
			bank.Balance = newBalance.Value;
			Dirty(mobUid, bank);
			_log.Info($"{mobUid} deposited {amount}");
			return true;
		}

		return false;
	}

	/// <summary>
	/// Attempts to remove money from a character's bank account without a backing entity.
	/// This should only be used in cases where a character doesn't have a backing entity.
	/// </summary>
	/// <param name="session">The session of the player making the withdrawal.</param>
	/// <param name="prefs">The preferences storing the character whose bank will be changed.</param>
	/// <param name="profile">The profile of the character whose account is being withdrawn.</param>
	/// <param name="amount">The number of spesos to be withdrawn.</param>
	/// <param name="newBalance">The new value of the bank account.</param>
	/// <returns>true if the transaction was successful, false if it was not.  When successful, newBalance contains the character's new balance.</returns>
	public bool TryBankWithdraw(ICommonSession session, PlayerPreferences prefs, HumanoidCharacterProfile profile, int amount, [NotNullWhen(true)] out int? newBalance)
	{
		newBalance = null; // Default return
		if (amount <= 0)
		{
			_log.Info($"TryBankWithdraw: {amount} is invalid");
			return false;
		}

		int balance = profile.BankBalance;

		if (balance < amount)
		{
			_log.Info($"TryBankWithdraw: {session.UserId} tried to withdraw {amount}, but has insufficient funds ({balance})");
			return false;
		}

		balance -= amount;

		var newProfile = profile.WithBankBalance(balance);
		var index = prefs.IndexOfCharacter(profile);
		if (index == -1)
		{
			_log.Info($"TryBankWithdraw: {session.UserId} tried to adjust the balance of {profile.Name}, but they were not in the user's character set.");
			return false;
		}
		_prefsManager.SetProfile(session.UserId, index, newProfile, validateFields: false);
		newBalance = balance;
		// Update any active admin UI with new balance
		RaiseLocalEvent(new BalanceChangedEvent(session, newBalance.Value));
		return true;
	}

	/// <summary>
	/// Attempts to add money to a character's bank account.
	/// This should only be used in cases where a character doesn't have a backing entity.
	/// </summary>
	/// <param name="session">The session of the player making the deposit.</param>
	/// <param name="prefs">The preferences storing the character whose bank will be changed.</param>
	/// <param name="profile">The profile of the character whose account is being withdrawn.</param>
	/// <param name="amount">The number of spesos to be deposited.</param>
	/// <param name="newBalance">The new value of the bank account.</param>
	/// <returns>true if the transaction was successful, false if it was not.  When successful, newBalance contains the character's new balance.</returns>
	public bool TryBankDeposit(ICommonSession session, PlayerPreferences prefs, HumanoidCharacterProfile profile, int amount, [NotNullWhen(true)] out int? newBalance)
	{
		newBalance = null; // Default return
		if (amount <= 0)
		{
			_log.Info($"TryBankDeposit: {amount} is invalid");
			return false;
		}

		// Lua: Apply deposit priority so Due/Hold reduce before balance increases
		newBalance = ApplyFinancePriorityDeposit(session, amount) ?? (profile.BankBalance + amount);

		var newProfile = profile.WithBankBalance(newBalance.Value);
		var index = prefs.IndexOfCharacter(profile);
		if (index == -1)
		{
			_log.Info($"{session.UserId} tried to adjust the balance of {profile.Name}, but they were not in the user's character set.");
			return false;
		}
		_prefsManager.SetProfile(session.UserId, index, newProfile, validateFields: false);
		// Update any active admin UI with new balance
		RaiseLocalEvent(new BalanceChangedEvent(session, newBalance.Value));
		return true;
	}

	/// <summary>
	/// Attempts to remove money from an offline character's bank account.
	/// This method works with offline players by directly modifying their preferences and saving to the database.
	/// </summary>
	/// <param name="userId">The NetUserId of the offline player</param>
	/// <param name="prefs">The player's preferences</param>
	/// <param name="profile">The character profile to modify</param>
	/// <param name="amount">The amount to withdraw</param>
	/// <returns>true if the transaction was successful, false if it was not</returns>
	public async Task<bool> TryBankWithdrawOffline(NetUserId userId, PlayerPreferences prefs, HumanoidCharacterProfile profile, int amount)
	{
		if (amount <= 0)
		{
			_log.Info($"TryBankWithdrawOffline: {amount} is invalid");
			return false;
		}

		int balance = profile.BankBalance;

		if (balance < amount)
		{
			_log.Info($"TryBankWithdrawOffline: {userId} tried to withdraw {amount}, but has insufficient funds ({balance})");
			return false;
		}

		balance -= amount;

		var newProfile = profile.WithBankBalance(balance);
		var index = prefs.IndexOfCharacter(profile);
		if (index == -1)
		{
			_log.Info($"TryBankWithdrawOffline: {userId} tried to adjust the balance of {profile.Name}, but they were not in the user's character set.");
			return false;
		}

		// Update preferences in cache if the player data exists
		if (_prefsManager.TryGetCachedPreferences(userId, out var cachedPrefs))
		{
			_prefsManager.SetProfile(userId, index, newProfile);
		}
		else
		{
			// If not in cache, save directly to database
			await _db.SaveCharacterSlotAsync(userId, newProfile, index);
		}

		_log.Info($"Offline player {userId} withdrew {amount}");
		return true;
	}

	/// <summary>
	/// Attempts to add money to an offline character's bank account.
	/// This method works with offline players by directly modifying their preferences and saving to the database.
	/// </summary>
	/// <param name="userId">The NetUserId of the offline player</param>
	/// <param name="prefs">The player's preferences</param>
	/// <param name="profile">The character profile to modify</param>
	/// <param name="amount">The amount to deposit</param>
	/// <returns>true if the transaction was successful, false if it was not</returns>
	public async Task<bool> TryBankDepositOffline(NetUserId userId, PlayerPreferences prefs, HumanoidCharacterProfile profile, int amount)
	{
		if (amount <= 0)
		{
			_log.Info($"TryBankDepositOffline: {amount} is invalid");
			return false;
		}

		int newBalance = profile.BankBalance + amount;

		var newProfile = profile.WithBankBalance(newBalance);
		var index = prefs.IndexOfCharacter(profile);

		// Lua: Apply finance priority even for offline deposits
		amount = ApplyFinancePriorityOfflineDeposit(userId, index, amount) ?? amount;
		newBalance = profile.BankBalance + amount;
		newProfile = profile.WithBankBalance(newBalance);
		if (index == -1)
		{
			_log.Info($"TryBankDepositOffline: {userId} tried to adjust the balance of {profile.Name}, but they were not in the user's character set.");
			return false;
		}

		// Update preferences in cache if the player data exists
		if (_prefsManager.TryGetCachedPreferences(userId, out var cachedPrefs))
		{
			_prefsManager.SetProfile(userId, index, newProfile);
		}
		else
		{
			// If not in cache, save directly to database
			await _db.SaveCharacterSlotAsync(userId, newProfile, index);
		}

		_log.Info($"Offline player {userId} deposited {amount}");
		return true;
	}


	/// <summary>
	/// Retrieves a character's balance via its in-game entity, if it has one.
	/// </summary>
	/// <param name="ent">The UID that the bank account is connected to, typically the player controlled mob</param>
	/// <param name="balance">When successful, contains the account balance in spesos. Otherwise, set to 0.</param>
	/// <returns>true if the account was successfully queried.</returns>
	public bool TryGetBalance(EntityUid ent, out int balance)
	{
		if (!_playerManager.TryGetSessionByEntity(ent, out var session) ||
			!_prefsManager.TryGetCachedPreferences(session.UserId, out var prefs))
		{
			_log.Info($"{ent} has no cached prefs");
			balance = 0;
			return false;
		}

		if (prefs.SelectedCharacter is not HumanoidCharacterProfile profile)
		{
			_log.Info($"{ent} has the wrong prefs type");
			balance = 0;
			return false;
		}

		balance = profile.BankBalance;
		return true;
	}

	/// <summary>
	/// Retrieves a character's balance via a player's session.
	/// </summary>
	/// <param name="session">The session of the player character to query.</param>
	/// <param name="balance">When successful, contains the account balance in spesos. Otherwise, set to 0.</param>
	/// <returns>true if the account was successfully queried.</returns>
	public bool TryGetBalance(ICommonSession session, out int balance)
	{
		if (!_prefsManager.TryGetCachedPreferences(session.UserId, out var prefs))
		{
			_log.Info($"{session.UserId} has no cached prefs");
			balance = 0;
			return false;
		}

		if (prefs.SelectedCharacter is not HumanoidCharacterProfile profile)
		{
			_log.Info($"{session.UserId} has the wrong prefs type");
			balance = 0;
			return false;
		}

		balance = profile.BankBalance;
		return true;
	}

	/// <summary>
	/// Update the bank balance to the character's current account balance.
	/// </summary>
	private void UpdateBankBalance(EntityUid mobUid, BankAccountComponent comp)
	{
		if (TryGetBalance(mobUid, out var balance))
			comp.Balance = balance;
		else
			comp.Balance = 0;

		Dirty(mobUid, comp);
	}

	/// <summary>
	/// Component initialized - if the player exists in the entity before the BankAccountComponent, update the player's account.
	/// </summary>
	public void OnInit(EntityUid mobUid, BankAccountComponent comp, ComponentInit _)
	{
		UpdateBankBalance(mobUid, comp);
	}

	/// <summary>
	/// Player's preferences loaded (mostly for hotjoin)
	/// </summary>
	        public void OnPreferencesLoaded(EntityUid mobUid, BankAccountComponent comp, PreferencesLoadedEvent ev) //Lua
        {
            UpdateBankBalance(mobUid, comp);
            HandleYupiCodeAssignmentSafely(ev); //Lua
        }

	/// <summary>
	/// Player attached, make sure the bank account is up-to-date.
	/// </summary>
	public void OnPlayerAttached(EntityUid mobUid, BankAccountComponent comp, PlayerAttachedEvent _)
	{
		UpdateBankBalance(mobUid, comp);
	}

	/// <summary>
	/// Player detached, make sure the bank account is up-to-date.
	/// </summary>
	public void OnPlayerDetached(EntityUid mobUid, BankAccountComponent comp, PlayerDetachedEvent _)
	{
		UpdateBankBalance(mobUid, comp);
	}

	/// <summary>
	/// Ensures the bank account listed in the lobby is accurate by ensuring the preferences cache is up-to-date.
	/// </summary>
	private void OnPlayerLobbyJoin(PlayerJoinedLobbyEvent args)
	{
		var cts = new CancellationToken();
		_prefsManager.RefreshPreferencesAsync(args.PlayerSession, cts);
	}

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
