// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Lua.Server.Bank;
using Content.Server.Chat.Managers;
using Content.Server.Popups;
using Content.Shared.Chat;
using Content.Lua.Common.CLVar;
using Content.Shared.Popups;
using Robust.Server.ServerStatus;
using Robust.Shared.Asynchronous;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using System.Net;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Content.Lua.Server.Transfers;

public sealed class TransferApiSystem : EntitySystem
{
    [Dependency] private readonly IStatusHost _statusHost = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly BankSystem _bankSystem = default!;
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
            if (context.RequestMethod != HttpMethod.Post || context.Url.AbsolutePath != "/api/bank/credit")
                return false;
            if (!await CheckAccess(context))
                return true;
            await HandleCredit(context);
            return true;
        });
        _statusHost.AddHandler(async context =>
        {
            if (context.RequestMethod != HttpMethod.Post || context.Url.AbsolutePath != "/api/bank/debit")
                return false;
            if (!await CheckAccess(context))
                return true;
            await HandleDebit(context);
            return true;
        });
    }

    private async Task<bool> CheckAccess(IStatusHandlerContext context)
    {
        if (!context.RequestHeaders.TryGetValue("X-Api-Secret", out var secretHeader))
        {
            await context.RespondAsync(Loc.GetString("transfer-api-auth-required"), HttpStatusCode.Unauthorized);
            return false;
        }

        var secret = _config.GetCVar(CLVars.TransferApiSecret);
        if (string.IsNullOrWhiteSpace(secret) || secret != secretHeader.ToString())
        {
            _sawmill.Warning("Unauthorized transfer API access attempt from {RemoteEndPoint}", context.RemoteEndPoint);
            await context.RespondAsync(Loc.GetString("transfer-api-auth-invalid"), HttpStatusCode.Unauthorized);
            return false;
        }

        return true;
    }

    private async Task<T?> ReadJsonRequest<T>(IStatusHandlerContext context, string requestName) where T : class
    {
        try
        {
            return await context.RequestBodyJsonAsync<T>();
        }
        catch (Exception ex)
        {
            _sawmill.Warning("Failed to deserialize {RequestName} request from {RemoteEndPoint}: {Exception}", requestName, context.RemoteEndPoint, ex);
            return null;
        }
    }

    private async Task HandleCredit(IStatusHandlerContext context)
    {
        try
        {
            var request = await ReadJsonRequest<BankAmountRequest>(context, "credit");
            if (request == null || request.Amount <= 0)
            {
                await context.RespondAsync(Loc.GetString("transfer-api-invalid-request-body"), HttpStatusCode.BadRequest);
                return;
            }

            _sawmill.Info("Processing credit request: UserId={UserId}, Amount={Amount}", request.UserId, request.Amount);
            var result = await RunOnMainThread(async () =>
            {
                if (!Guid.TryParse(request.UserId, out var userId))
                {
                    _sawmill.Warning("Invalid UserId format: {UserId}", request.UserId);
                    return new TransferExecuteResponse { Success = false, Message = Loc.GetString("transfer-api-invalid-user-id") };
                }

                var netUserId = new NetUserId(userId);
                var ok = await _bankSystem.TryBankDepositOffline(netUserId, request.Amount);
                if (!ok)
                    return new TransferExecuteResponse { Success = false, Message = Loc.GetString("transfer-api-deposit-failed") };

                if (_playerManager.TryGetSessionById(netUserId, out var session) &&
                    _bankSystem.TryGetBalance(session, out var finalBalance) &&
                    session.AttachedEntity is { Valid: true } entityUid)
                {
                    NotifyCredit(entityUid, request.Amount, finalBalance);
                }

                _sawmill.Info("Credit executed: {UserId}, Amount: {Amount}", userId, request.Amount);
                return new TransferExecuteResponse { Success = true };
            });

            if (result.Success)
                await context.RespondJsonAsync(result);
            else
                await context.RespondJsonAsync(result, HttpStatusCode.BadRequest);
        }
        catch (Exception ex)
        {
            _sawmill.Error("Error handling credit request: {Exception}", ex);
            await context.RespondAsync(Loc.GetString("transfer-api-internal-error"), HttpStatusCode.InternalServerError);
        }
    }

    private async Task HandleDebit(IStatusHandlerContext context)
    {
        try
        {
            var request = await ReadJsonRequest<BankAmountRequest>(context, "debit");
            if (request == null || request.Amount <= 0)
            {
                await context.RespondAsync(Loc.GetString("transfer-api-invalid-request-body"), HttpStatusCode.BadRequest);
                return;
            }

            _sawmill.Info("Processing debit request: UserId={UserId}, Amount={Amount}", request.UserId, request.Amount);
            var result = await RunOnMainThread(async () =>
            {
                if (!Guid.TryParse(request.UserId, out var userId))
                {
                    _sawmill.Warning("Invalid UserId format: {UserId}", request.UserId);
                    return new TransferExecuteResponse { Success = false, Message = Loc.GetString("transfer-api-invalid-user-id") };
                }

                var netUserId = new NetUserId(userId);
                var ok = await _bankSystem.TryBankWithdrawOffline(netUserId, request.Amount);
                if (!ok)
                    return new TransferExecuteResponse { Success = false, Message = Loc.GetString("transfer-api-withdraw-failed") };

                if (_playerManager.TryGetSessionById(netUserId, out var session) &&
                    _bankSystem.TryGetBalance(session, out var finalBalance) &&
                    session.AttachedEntity is { Valid: true } entityUid)
                {
                    NotifyDebit(entityUid, request.Amount, finalBalance);
                }

                _sawmill.Info("Debit executed: {UserId}, Amount: {Amount}", userId, request.Amount);
                return new TransferExecuteResponse { Success = true };
            });

            if (result.Success)
                await context.RespondJsonAsync(result);
            else
                await context.RespondJsonAsync(result, HttpStatusCode.BadRequest);
        }
        catch (Exception ex)
        {
            _sawmill.Error("Error handling debit request: {Exception}", ex);
            await context.RespondAsync(Loc.GetString("transfer-api-internal-error"), HttpStatusCode.InternalServerError);
        }
    }

    private async Task<T> RunOnMainThread<T>(Func<Task<T>> func)
    {
        var taskCompletionSource = new TaskCompletionSource<T>();
        _taskManager.RunOnMainThread(async () =>
        {
            try
            {
                var result = await func();
                taskCompletionSource.TrySetResult(result);
            }
            catch (Exception e)
            {
                taskCompletionSource.TrySetException(e);
            }
        });
        return await taskCompletionSource.Task;
    }

    private sealed record BankAmountRequest(
        [property: JsonPropertyName("userId")] string UserId,
        [property: JsonPropertyName("amount")] int Amount
    );

    private sealed record TransferExecuteResponse(bool Success = false, string? Message = null);

    private void NotifyDebit(EntityUid entity, int amount, int newBalance)
    {
        if (!_playerManager.TryGetSessionByEntity(entity, out var session))
            return;
        var message = Loc.GetString("transfer-api-notify-deposit-sent", ("amount", amount), ("balance", newBalance), ("currency", "$"));
        _popup.PopupEntity(message, entity, Filter.Entities(entity), true, PopupType.Small);
        _chatManager.ChatMessageToOne(ChatChannel.Notifications, message, message, EntityUid.Invalid, false, session.Channel);
    }

    private void NotifyCredit(EntityUid entity, int amount, int newBalance)
    {
        if (!_playerManager.TryGetSessionByEntity(entity, out var session))
            return;
        var message = Loc.GetString("transfer-api-notify-withdraw-received", ("amount", amount), ("balance", newBalance), ("currency", "$"));
        _popup.PopupEntity(message, entity, Filter.Entities(entity), true, PopupType.Small);
        _chatManager.ChatMessageToOne(ChatChannel.Notifications, message, message, EntityUid.Invalid, false, session.Channel);
    }
}
