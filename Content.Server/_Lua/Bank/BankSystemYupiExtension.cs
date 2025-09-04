/*
 * LuaWorld - This file is licensed under AGPLv3
 * Copyright (c) 2025 LuaWorld Contributors
 * See AGPLv3.txt for details.
 */
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
/// Partial расширение BankSystem для функциональности ЮПИ и интеграции с финансовой системой
/// </summary>
public sealed partial class BankSystem
{
    /// <summary>
    /// Применяет приоритет финансовой системы при депозите (Due/Hold уменьшаются перед увеличением баланса)
    /// </summary>
    private int? ApplyFinancePriorityDeposit(ICommonSession session, int amount)
    {
        // Временно отключено - финансовая система не найдена
        return null;
    }

    /// <summary>
    /// Применяет приоритет финансовой системы для оффлайн депозитов
    /// </summary>
    private int? ApplyFinancePriorityOfflineDeposit(NetUserId userId, int characterIndex, int amount)
    {
        // Временно отключено - финансовая система не найдена
        return null;
    }

    /// <summary>
    /// Проверяет максимальную сумму перевода из CVar
    /// </summary>
    private bool CheckTransferLimit(int amount)
    {
        // Временно отключено - FinanceCVars не найден
        // var cvarMax = IoCManager.Resolve<IConfigurationManager>().GetCVar(FinanceCVars.TransferMaxAmountPerOperation);
        // return amount <= cvarMax;
        return true; // Разрешаем все переводы пока не исправим
    }

    /// <summary>
    /// Безопасная обработка загрузки предпочтений без async void
    /// </summary>
    private void HandleYupiCodeAssignmentSafely(PreferencesLoadedEvent ev)
    {
        // Запускаем асинхронную работу в фоне безопасно
        _ = Task.Run(async () =>
        {
            try
            {
                var prefs = ev.Prefs;
                // Assign codes to any slots missing one.
                var assigned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var (idx, profBase) in prefs.Characters)
                {
                    if (profBase is not HumanoidCharacterProfile profile)
                        continue;
                    if (IsValidYupiCode(profile.YupiAccountCode))
                    {
                        assigned.Add(profile.YupiAccountCode);
                        continue;
                    }

                    // Generate a unique code and save it to this slot
                    var code = await GenerateUniqueYupiCodeAsync();
                    while (assigned.Contains(code))
                    {
                        code = await GenerateUniqueYupiCodeAsync();
                    }
                    assigned.Add(code);
                    var newProfile = profile.WithYupiAccountCode(code);
                    await _prefsManager.SetProfile(ev.Session.UserId, idx, newProfile, validateFields: false);
                }
            }
            catch (Exception e)
            {
                _log.Error($"OnPreferencesLoaded YUPI assignment failed: {e}");
            }
        });
    }

    /// <summary>
    /// Shim для оффлайн сессий при работе с финансовой системой
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
