// LuaWorld/LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld/LuaCorp
// See AGPLv3.txt for details.

using Content.Server._NF.Bank;
using Content.Server.Chat.Managers;
using Content.Server.Database;
using Content.Server.Popups;
using Content.Server.Preferences.Managers;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Chat;
using Content.Shared.Lua.CLVar;
using Content.Shared.Popups;
using Content.Shared.Preferences;
using Microsoft.EntityFrameworkCore;
using Robust.Server.ServerStatus;
using Robust.Shared.Asynchronous;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Content.Server._Lua.Transfers;

public sealed class TransferApiSystem : EntitySystem
{
    [Dependency] private readonly IStatusHost _statusHost = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IServerPreferencesManager _prefsManager = default!;
    [Dependency] private readonly BankSystem _bankSystem = default!;
    [Dependency] private readonly IServerDbManager _dbManager = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly ITaskManager _taskManager = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logManager.GetSawmill("transferapi");
        _statusHost.AddHandler(async context =>
        {
            if (context.RequestMethod != HttpMethod.Post || context.Url.AbsolutePath != "/api/transfers/withdraw") return false;
            if (!await CheckAccess(context)) return true;
            await HandleWithdraw(context); return true;
        });
        _statusHost.AddHandler(async context =>
        {
            if (context.RequestMethod != HttpMethod.Post || context.Url.AbsolutePath != "/api/transfers/deposit") return false;
            if (!await CheckAccess(context)) return true;
            await HandleDeposit(context); return true;
        });
    }

    private async Task<bool> CheckAccess(IStatusHandlerContext context)
    {
        if (!context.RequestHeaders.TryGetValue("X-Api-Secret", out var secretHeader))
        { await context.RespondAsync(Loc.GetString("transfer-api-auth-required"), HttpStatusCode.Unauthorized); return false; }
        var secret = _config.GetCVar(CLVars.TransferApiSecret);
        if (string.IsNullOrWhiteSpace(secret) || secret != secretHeader.ToString())
        {
            _sawmill.Warning("Unauthorized transfer API access attempt from {RemoteEndPoint}", context.RemoteEndPoint);
            await context.RespondAsync(Loc.GetString("transfer-api-auth-invalid"), HttpStatusCode.Unauthorized);
            return false;
        }
        return true;
    }

    private sealed record ResolvedHumanoidProfile(int Slot, PlayerPreferences Prefs, HumanoidCharacterProfile Profile);

    private async Task<T?> ReadJsonRequest<T>(IStatusHandlerContext context, string requestName) where T : class
    {
        try
        { return await context.RequestBodyJsonAsync<T>(); }
        catch (Exception ex)
        { _sawmill.Warning("Failed to deserialize {RequestName} request from {RemoteEndPoint}: {Exception}", requestName, context.RemoteEndPoint, ex); return null; }
    }

    private async Task<ResolvedHumanoidProfile?> ResolveHumanoidProfileAsync(NetUserId userId, int profileId)
    {
        var slot = await GetProfileSlotByIdAsync(userId, profileId);
        if (slot == null) return null;
        if (_prefsManager.TryGetCachedPreferences(userId, out var cachedPrefs) && cachedPrefs.Characters.TryGetValue(slot.Value, out var cachedChar) && cachedChar is HumanoidCharacterProfile cachedProfile)
        { return new ResolvedHumanoidProfile(slot.Value, cachedPrefs, cachedProfile); }
        var prefs = await _dbManager.GetPlayerPreferencesAsync(userId, default);
        if (prefs == null) return null;
        if (!prefs.Characters.TryGetValue(slot.Value, out var character)) return null;
        if (character is not HumanoidCharacterProfile loadedProfile) return null;
        return new ResolvedHumanoidProfile(slot.Value, prefs, loadedProfile);
    }

    private async Task HandleWithdraw(IStatusHandlerContext context)
    {
        try
        {
            var request = await ReadJsonRequest<WithdrawRequest>(context, "withdraw");
            if (request == null)
            { await context.RespondAsync(Loc.GetString("transfer-api-invalid-request-body"), HttpStatusCode.BadRequest); return; }
            _sawmill.Info("Processing withdraw request: UserId={UserId}, ProfileId={ProfileId}, Amount={Amount}", request.UserId, request.ProfileId, request.Amount);
            var result = await RunOnMainThread(async () =>
            {
                if (!Guid.TryParse(request.UserId, out var userId))
                {
                    _sawmill.Warning("Invalid UserId format: {UserId}", request.UserId);
                    return new TransferExecuteResponse { Success = false, Message = Loc.GetString("transfer-api-invalid-user-id") };
                }
                var netUserId = new NetUserId(userId);
                var resolved = await ResolveHumanoidProfileAsync(netUserId, request.ProfileId);
                if (resolved == null)
                {
                    _sawmill.Warning("Profile not found: UserId={UserId}, ProfileId={ProfileId}", netUserId, request.ProfileId);
                    return new TransferExecuteResponse { Success = false, Message = Loc.GetString("transfer-api-profile-not-found") };
                }
                var slot = resolved.Slot;
                var profile = resolved.Profile;
                var prefs = resolved.Prefs;
                int currentBalance = profile.BankBalance;
                int newBalance = currentBalance + request.Amount;
                _sawmill.Debug("Withdraw: ProfileId={ProfileId}, Slot={Slot}, CurrentBalance={CurrentBalance}, Amount={Amount}, ExpectedNewBalance={NewBalance}", request.ProfileId, slot, currentBalance, request.Amount, newBalance);
                bool isActiveCharacter = false;
                int? selectedSlot = null;
                EntityUid? entityUid = null;
                if (_playerManager.TryGetSessionById(netUserId, out var session))
                {
                    entityUid = session.AttachedEntity;
                    if (_prefsManager.TryGetCachedPreferences(netUserId, out var sessionCachedPrefs))
                    {
                        selectedSlot = sessionCachedPrefs.SelectedCharacterIndex;
                        isActiveCharacter = sessionCachedPrefs.SelectedCharacterIndex == slot;
                        _sawmill.Debug("Withdraw: Player online, SelectedSlot={SelectedSlot}, RequestedSlot={RequestedSlot}, IsActive={IsActive}, EntityUid={EntityUid}", selectedSlot, slot, isActiveCharacter, entityUid);
                    }
                    else { _sawmill.Debug("Withdraw: Player online but no cached preferences, EntityUid={EntityUid}", entityUid); }
                }
                else { _sawmill.Debug("Withdraw: Player offline"); }
                bool depositResult;
                if (isActiveCharacter && entityUid != null && TryComp<BankAccountComponent>(entityUid.Value, out var bank))
                {
                    var entityBalance = bank.Balance;
                    var balanceMatches = entityBalance == currentBalance;
                    _sawmill.Debug("Withdraw: Using online method check, EntityUid={EntityUid}, EntityBalance={EntityBalance}, ProfileBalance={ProfileBalance}, BalanceMatches={BalanceMatches}", entityUid.Value, entityBalance, currentBalance, balanceMatches);
                    if (balanceMatches)
                    {
                        depositResult = _bankSystem.TryBankDeposit(entityUid.Value, request.Amount);
                        if (depositResult)
                        {
                            var finalBalance = EntityManager.GetComponent<BankAccountComponent>(entityUid.Value).Balance;
                            _sawmill.Debug("Withdraw: Online method succeeded, FinalBalance={FinalBalance}", finalBalance);
                            NotifyWithdrawReceived(entityUid.Value, request.Amount, finalBalance);
                        }
                        else { _sawmill.Warning("Withdraw: Online method failed"); }
                    }
                    else
                    {
                        _sawmill.Debug("Withdraw: Balance mismatch detected, switching to offline method. EntityBalance={EntityBalance}, ProfileBalance={ProfileBalance}", entityBalance, currentBalance);
                        depositResult = await _bankSystem.TryBankDepositOffline(netUserId, prefs, profile, request.Amount);
                        if (depositResult)
                        { _sawmill.Debug("Withdraw: Offline method succeeded, ExpectedNewBalance={NewBalance}", newBalance); }
                        else { _sawmill.Warning("Withdraw: Offline method failed"); }
                    }
                }
                else
                {
                    _sawmill.Debug("Withdraw: Using offline method (IsActive={IsActive}, EntityUid={EntityUid})", isActiveCharacter, entityUid);
                    depositResult = await _bankSystem.TryBankDepositOffline(netUserId, prefs, profile, request.Amount);
                    if (depositResult)
                    { _sawmill.Debug("Withdraw: Offline method succeeded, ExpectedNewBalance={NewBalance}", newBalance); }
                    else { _sawmill.Warning("Withdraw: Offline method failed"); }
                }
                if (!depositResult) { return new TransferExecuteResponse { Success = false, Message = Loc.GetString("transfer-api-deposit-failed") }; }
                _sawmill.Info("Withdraw executed: {UserId} ({Profile}), Amount: {Amount}", userId, profile.Name, request.Amount);
                return new TransferExecuteResponse { Success = true };
            });
            if (result.Success)
            { await context.RespondJsonAsync(result); }
            else { await context.RespondJsonAsync(result, HttpStatusCode.BadRequest); }
        }
        catch (Exception ex)
        {
            _sawmill.Error("Error handling withdraw request: {Exception}", ex);
            await context.RespondAsync(Loc.GetString("transfer-api-internal-error"), HttpStatusCode.InternalServerError);
        }
    }

    private async Task HandleDeposit(IStatusHandlerContext context)
    {
        try
        {
            var request = await ReadJsonRequest<DepositRequest>(context, "deposit");
            if (request == null)
            { await context.RespondAsync(Loc.GetString("transfer-api-invalid-request-body"), HttpStatusCode.BadRequest); return; }
            _sawmill.Info("Processing deposit request: UserId={UserId}, ProfileId={ProfileId}, Amount={Amount}", request.UserId, request.ProfileId, request.Amount);
            var result = await RunOnMainThread(async () =>
            {
                if (!Guid.TryParse(request.UserId, out var userId))
                {
                    _sawmill.Warning("Invalid UserId format: {UserId}", request.UserId);
                    return new TransferExecuteResponse { Success = false, Message = Loc.GetString("transfer-api-invalid-user-id") };
                }
                var netUserId = new NetUserId(userId);
                var resolved = await ResolveHumanoidProfileAsync(netUserId, request.ProfileId);
                if (resolved == null)
                {
                    _sawmill.Warning("Profile not found: UserId={UserId}, ProfileId={ProfileId}", netUserId, request.ProfileId);
                    return new TransferExecuteResponse { Success = false, Message = Loc.GetString("transfer-api-profile-not-found") };
                }
                var slot = resolved.Slot;
                var profile = resolved.Profile;
                var prefs = resolved.Prefs;
                if (profile.BankBalance - request.Amount < 100000)
                { return new TransferExecuteResponse { Success = false, Message = Loc.GetString("transfer-api-insufficient-balance") }; }
                int currentBalance = profile.BankBalance;
                int newBalance = currentBalance - request.Amount;
                _sawmill.Debug("Deposit: ProfileId={ProfileId}, Slot={Slot}, CurrentBalance={CurrentBalance}, Amount={Amount}, ExpectedNewBalance={NewBalance}", request.ProfileId, slot, currentBalance, request.Amount, newBalance);
                bool isActiveCharacter = false;
                int? selectedSlot = null;
                EntityUid? entityUid = null;
                if (_playerManager.TryGetSessionById(netUserId, out var session))
                {
                    entityUid = session.AttachedEntity;
                    if (_prefsManager.TryGetCachedPreferences(netUserId, out var sessionCachedPrefs))
                    {
                        selectedSlot = sessionCachedPrefs.SelectedCharacterIndex;
                        isActiveCharacter = sessionCachedPrefs.SelectedCharacterIndex == slot;
                        _sawmill.Debug("Deposit: Player online, SelectedSlot={SelectedSlot}, RequestedSlot={RequestedSlot}, IsActive={IsActive}, EntityUid={EntityUid}", selectedSlot, slot, isActiveCharacter, entityUid);
                    }
                    else { _sawmill.Debug("Deposit: Player online but no cached preferences, EntityUid={EntityUid}", entityUid); }
                }
                else { _sawmill.Debug("Deposit: Player offline"); }
                bool withdrawResult;
                if (isActiveCharacter && entityUid != null && TryComp<BankAccountComponent>(entityUid.Value, out var bank))
                {
                    var entityBalance = bank.Balance;
                    var balanceMatches = entityBalance == currentBalance;
                    _sawmill.Debug("Deposit: Using online method check, EntityUid={EntityUid}, EntityBalance={EntityBalance}, ProfileBalance={ProfileBalance}, BalanceMatches={BalanceMatches}", entityUid.Value, entityBalance, currentBalance, balanceMatches);
                    if (balanceMatches)
                    {
                        withdrawResult = _bankSystem.TryBankWithdraw(entityUid.Value, request.Amount);
                        if (withdrawResult)
                        {
                            var finalBalance = EntityManager.GetComponent<BankAccountComponent>(entityUid.Value).Balance;
                            _sawmill.Debug("Deposit: Online method succeeded, FinalBalance={FinalBalance}", finalBalance);
                            NotifyDepositSent(entityUid.Value, request.Amount, finalBalance);
                        }
                        else { _sawmill.Warning("Deposit: Online method failed"); }
                    }
                    else
                    {
                        _sawmill.Debug("Deposit: Balance mismatch detected, switching to offline method. EntityBalance={EntityBalance}, ProfileBalance={ProfileBalance}", entityBalance, currentBalance);
                        withdrawResult = await _bankSystem.TryBankWithdrawOffline(netUserId, prefs, profile, request.Amount);
                        if (withdrawResult)
                        { _sawmill.Debug("Deposit: Offline method succeeded, ExpectedNewBalance={NewBalance}", newBalance); }
                        else { _sawmill.Warning("Deposit: Offline method failed"); }
                    }
                }
                else
                {
                    _sawmill.Debug("Deposit: Using offline method (IsActive={IsActive}, EntityUid={EntityUid})", isActiveCharacter, entityUid);
                    withdrawResult = await _bankSystem.TryBankWithdrawOffline(netUserId, prefs, profile, request.Amount);
                    if (withdrawResult)
                    { _sawmill.Debug("Deposit: Offline method succeeded, ExpectedNewBalance={NewBalance}", newBalance); }
                    else { _sawmill.Warning("Deposit: Offline method failed"); }
                }
                if (!withdrawResult)
                { return new TransferExecuteResponse { Success = false, Message = Loc.GetString("transfer-api-withdraw-failed") }; }
                _sawmill.Info("Deposit executed: {UserId} ({Profile}), Amount: {Amount}", userId, profile.Name, request.Amount);
                return new TransferExecuteResponse { Success = true };
            });
            if (result.Success)
            { await context.RespondJsonAsync(result); }
            else { await context.RespondJsonAsync(result, HttpStatusCode.BadRequest); }
        }
        catch (Exception ex)
        {
            _sawmill.Error("Error handling deposit request: {Exception}", ex);
            await context.RespondAsync(Loc.GetString("transfer-api-internal-error"), HttpStatusCode.InternalServerError);
        }
    }

    private async Task<int?> GetProfileSlotByIdAsync(NetUserId userId, int profileId) //WARN
    {
        try
        {
            _sawmill.Debug("Getting profile slot: UserId={UserId}, ProfileId={ProfileId}", userId, profileId);
            ServerDbBase? dbBase = null;
            if (_dbManager is ServerDbBase directDbBase)
            { dbBase = directDbBase; }
            else if (_dbManager is ServerDbManager dbManager)
            {
                var dbField = typeof(ServerDbManager).GetField("_db", BindingFlags.NonPublic | BindingFlags.Instance);
                if (dbField != null)
                { dbBase = dbField.GetValue(dbManager) as ServerDbBase; }
            }
            if (dbBase != null)
            {
                var getDbMethod = typeof(ServerDbBase).GetMethod("GetDb", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                if (getDbMethod != null)
                {
                    var dbTask = getDbMethod.Invoke(dbBase, new object?[] { default(CancellationToken), null });
                    if (dbTask is Task dbTaskTyped)
                    {
                        await dbTaskTyped.ConfigureAwait(false);
                        var db = dbTaskTyped.GetType().GetProperty("Result")?.GetValue(dbTaskTyped);
                        if (db != null)
                        {
                            var dbContextProperty = db.GetType().GetProperty("DbContext");
                            if (dbContextProperty?.GetValue(db) is ServerDbContext dbContext)
                            {
                                var profiles = await dbContext.Profile
                                    .Include(p => p.Preference)
                                    .Where(p => p.Preference.UserId == userId.UserId)
                                    .Select(p => new { p.Id, p.Slot })
                                    .ToListAsync();
                                _sawmill.Debug("Found {Count} profiles for user {UserId}.", profiles.Count, userId);
                                var matchingProfile = profiles.FirstOrDefault(p => p.Id == profileId);
                                var disposeMethod = db.GetType().GetMethod("DisposeAsync");
                                if (matchingProfile != null)
                                {
                                    _sawmill.Debug("Found profile with Id={ProfileId}, Slot={Slot} for user {UserId}", profileId, matchingProfile.Slot, userId);
                                    if (disposeMethod != null)
                                    { await (ValueTask)disposeMethod.Invoke(db, null)!; }
                                    return matchingProfile.Slot;
                                }
                                else
                                {
                                    _sawmill.Warning("Profile with Id={ProfileId} not found for user {UserId}.", profileId, userId);
                                    if (disposeMethod != null)
                                    { await (ValueTask)disposeMethod.Invoke(db, null)!; }
                                }
                            }
                        }
                    }
                }
            }
            else { _sawmill.Warning("Could not get ServerDbBase from dbManager, type: {Type}", _dbManager?.GetType().FullName); }
        }
        catch (Exception ex)
        { _sawmill.Error("Error getting profile slot by ID: UserId={UserId}, ProfileId={ProfileId}, Exception={Exception}", userId, profileId, ex); }
        return null;
    }

    private async Task<T> RunOnMainThread<T>(Func<Task<T>> func)
    {
        var taskCompletionSource = new TaskCompletionSource<T>();
        _taskManager.RunOnMainThread(async () =>
        {
            try
            { var result = await func(); taskCompletionSource.TrySetResult(result); }
            catch (Exception e)
            { taskCompletionSource.TrySetException(e); }
        });
        return await taskCompletionSource.Task;
    }

    private sealed record WithdrawRequest(
        [property: JsonPropertyName("userId")] string UserId,
        [property: JsonPropertyName("profileId")] int ProfileId,
        [property: JsonPropertyName("amount")] int Amount
    );

    private sealed record DepositRequest(
        [property: JsonPropertyName("userId")] string UserId,
        [property: JsonPropertyName("profileId")] int ProfileId,
        [property: JsonPropertyName("amount")] int Amount
    );

    private sealed record TransferExecuteResponse(bool Success = false, string? Message = null);

    private void NotifyDepositSent(EntityUid entity, int amount, int newBalance)
    {
        if (!_playerManager.TryGetSessionByEntity(entity, out var session)) return;
        var message = Loc.GetString("transfer-api-notify-deposit-sent", ("amount", amount), ("balance", newBalance), ("currency", "$"));
        _popup.PopupEntity(message, entity, Filter.Entities(entity), true, PopupType.Small);
        _chatManager.ChatMessageToOne(ChatChannel.Notifications, message, message, EntityUid.Invalid, false, session.Channel);
    }

    private void NotifyWithdrawReceived(EntityUid entity, int amount, int newBalance)
    {
        if (!_playerManager.TryGetSessionByEntity(entity, out var session)) return;
        var message = Loc.GetString("transfer-api-notify-withdraw-received", ("amount", amount), ("balance", newBalance), ("currency", "$"));
        _popup.PopupEntity(message, entity, Filter.Entities(entity), true, PopupType.Small);
        _chatManager.ChatMessageToOne(ChatChannel.Notifications, message, message, EntityUid.Invalid, false, session.Channel);
    }
}

