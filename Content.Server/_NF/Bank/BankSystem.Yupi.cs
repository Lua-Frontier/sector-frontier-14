using Content.Shared._NF.Bank;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Preferences;
using Robust.Shared.Network;
using Robust.Shared.Player;
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
			// Mix letters and digits roughly evenly //Lua
			var pickDigit = rand.Next(0, 2) == 0;
			if (pickDigit)
				buf[i] = YupiDigits[rand.Next(YupiDigits.Length)];
			else
				buf[i] = YupiLetters[rand.Next(YupiLetters.Length)];
		}
		return new string(buf);
	}

	private string GenerateUniqueYupiCode()
	{
		// Build a HashSet of existing codes from active bank account components. //Lua
		var comparer = StringComparer.OrdinalIgnoreCase; //Lua
		var existing = new HashSet<string>(comparer); //Lua
		//Lua Start: avoid tuple deconstruction; iterate over component instances only
		foreach (var comp in EntityQuery<BankAccountComponent>(true))
		{
			if (!string.IsNullOrEmpty(comp.YupiCode))
				existing.Add(comp.YupiCode);
		}
		//Lua End

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
	/// Ensures the session's attached entity has a valid YUPI stored on its bank account component.
	/// Returns existing or newly generated code, or empty string if unavailable.
	/// </summary>
	public string EnsureYupiForSessionSelected(ICommonSession session)
	{
		try
		{
			//Lua Start: rebind YUPI code to the owner's BankAccountComponent
			if (session.AttachedEntity is not { Valid: true } ent)
				return string.Empty;
			if (!TryComp<BankAccountComponent>(ent, out var bank))
				return string.Empty;
			if (IsValidYupiCode(bank.YupiCode))
				return bank.YupiCode;

			var code = GenerateUniqueYupiCode();
			bank.YupiCode = code;
			Dirty(ent, bank);
			return code;
			//Lua End
		}
		catch (Exception e)
		{
			_log.Warning($"EnsureYupiForSessionSelected failed: {e.Message}");
			return string.Empty;
		}
	}

	/// <summary>
	/// Ensures the given entity with a bank account has a YUPI code and returns it.
	/// Returns empty string if the entity has no BankAccountComponent.
	/// </summary>
	public string EnsureYupiForEntity(EntityUid ent)
	{
		if (!TryComp<BankAccountComponent>(ent, out var bank))
			return string.Empty;
		if (IsValidYupiCode(bank.YupiCode))
			return bank.YupiCode;
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
			//Lua Start: iterate all entities with BankAccountComponent and assign codes if empty
			var enumerator = EntityQueryEnumerator<BankAccountComponent>();
			while (enumerator.MoveNext(out var uid, out var bank))
			{
				try
				{
					if (IsValidYupiCode(bank.YupiCode))
						continue;
					var code = GenerateUniqueYupiCode();
					bank.YupiCode = code;
					Dirty(uid, bank);
				}
				catch (Exception ex)
				{
					_log.Warning($"YUPI migration: failed for entity {uid}: {ex.Message}");
				}
			}
			_log.Info("YUPI migration: done.");
			//Lua End
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
			if (!TryComp<BankAccountComponent>(ent, out var bank)) //Lua: check component instead of profile
				continue; //Lua
			if (string.Equals(bank.YupiCode, norm, StringComparison.OrdinalIgnoreCase)) //Lua
			{
				target = ent;
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

		//Lua: check per-transfer limit from CVar
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

		if (!TryResolveOnlineByYupiCode(targetCodeInput, out var target, out _)) //Lua: resolve without profiles
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
			//Lua Start: obtain receiver code from component
			if (TryComp<BankAccountComponent>(target, out var tBank))
				receiverCode = tBank.YupiCode;
			//Lua End
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
		//Lua Start: obtain receiver code from component
		if (TryComp<BankAccountComponent>(target, out var tBank2))
			receiverCode = tBank2.YupiCode;
		//Lua End
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
