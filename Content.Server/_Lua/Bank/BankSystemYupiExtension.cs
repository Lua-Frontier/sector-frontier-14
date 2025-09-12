/*
 * LuaWorld - This file is licensed under AGPLv3
 * Copyright (c) 2025 LuaWorld Contributors
 * See AGPLv3.txt for details.
 */
using System;
using System.Collections.Generic;
using Content.Server.Database;
using Content.Server.Preferences.Managers;
using Content.Shared._Lua.Finance;
using Content.Server._Lua.Finance;
using Content.Shared._NF.Bank;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Preferences;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;
using Robust.Shared.Player;
using Robust.Shared.Network;
using Robust.Shared.Enums;
using Robust.Shared.GameStates;
using Robust.Server.Player;

namespace Content.Server._NF.Bank;

/// <summary>
/// Partial extension of BankSystem for YUPI functionality and finance integration
/// </summary>
public sealed partial class BankSystem
{
	/// <summary>
	/// Applies finance system priority for deposits (reduce Due/Hold before increasing balance)
	/// </summary>
	private int? ApplyFinancePriorityDeposit(ICommonSession session, int amount)
	{
		// Temporarily disabled - finance system not found
		return null;
	}

	/// <summary>
	/// Applies finance system priority for offline deposits
	/// </summary>
	private int? ApplyFinancePriorityOfflineDeposit(NetUserId userId, int characterIndex, int amount)
	{
		// Temporarily disabled - finance system not found
		return null;
	}

	/// <summary>
	/// Checks the maximum transfer amount from CVar
	/// </summary>
	private bool CheckTransferLimit(int amount)
	{
		// Temporarily disabled - FinanceCVars not available
		// var cvarMax = IoCManager.Resolve<IConfigurationManager>().GetCVar(FinanceCVars.TransferMaxAmountPerOperation);
		// return amount <= cvarMax;
		return true; // Allow all transfers until configured
	}

	/// <summary>
	/// Preferences loaded handler. Ensure YUPI synchronously for the active session.
	/// </summary>
	private void HandleYupiCodeAssignmentSafely(PreferencesLoadedEvent ev)
	{
		try
		{
			// Ensure YUPI code on the attached entity (if any). This is best-effort.
			EnsureYupiForSessionSelected(ev.Session);
		}
		catch (Exception e)
		{
			_log.Error($"OnPreferencesLoaded YUPI ensure failed: {e}");
		}
	}

	/// <summary>
	/// Offline session shim for finance operations
	/// </summary>
	private sealed class OfflineSessionShim : ICommonSession
	{
		public NetUserId UserId { get; private set; }
		public EntityUid? AttachedEntity => null;
		public string Name => "OfflineShim";
		public short Ping => 0;
		public SessionStatus Status => SessionStatus.Disconnected;
		public DateTime ConnectedTime { get; set; } = DateTime.MinValue;
		public LoginType AuthType => LoginType.GuestAssigned;
		public INetChannel Channel { get; set; } = null!;
		public HashSet<EntityUid> ViewSubscriptions { get; } = new();
		public SessionState State { get; } = new();
		public SessionData Data { get; }
		public bool ClientSide { get; set; }

		public OfflineSessionShim(NetUserId userId)
		{
			UserId = userId;
			Data = new SessionData(UserId, Name);
		}

		public T? ContentData<T>() where T : class => null;
		public void SetData(object data) { }
		public void ClearData() { }
	}
}
