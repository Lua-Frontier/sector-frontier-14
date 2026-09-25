// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Content.Lua.Common.CLVar;
using Robust.Shared.Asynchronous;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Lua.Server.Bank;

public sealed partial class BankSystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly ITaskManager _taskManager = default!;

    private readonly Dictionary<NetUserId, string> _galBankCodeByUser = new();
    private readonly HashSet<NetUserId> _galBankCodeFetchInFlight = new();
    private readonly HashSet<NetUserId> _galBankTransferInFlight = new();
    private readonly Dictionary<NetUserId, TimeSpan> _galBankLastTransferAt = new();
    private readonly HttpClient _galBankHttp = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly TimeSpan GalBankTransferCooldown = TimeSpan.FromMinutes(1);

    private static readonly JsonSerializerOptions GalBankJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public enum GalBankTransferError
    {
        None,
        InvalidTarget,
        SelfTransfer,
        InvalidAmount,
        Cooldown,
        InsufficientFunds,
        NetworkError
    }

    public string GetCachedGalBankCode(NetUserId userId)
    {
        return _galBankCodeByUser.TryGetValue(userId, out var code) ? code : string.Empty;
    }

    public string EnsureGalBankForSessionSelected(ICommonSession session)
    {
        try
        {
            return EnsureGalBankForUser(session.UserId);
        }
        catch (Exception e)
        {
            _log.Warning($"EnsureGalBankForSessionSelected failed: {e.Message}");
            return string.Empty;
        }
    }

    public string EnsureGalBankForEntity(EntityUid ent)
    {
        if (!_playerManager.TryGetSessionByEntity(ent, out var session))
            return string.Empty;
        return EnsureGalBankForUser(session.UserId);
    }

    public string EnsureGalBankForUser(NetUserId userId)
    {
        if (_galBankCodeByUser.TryGetValue(userId, out var existing) && !string.IsNullOrWhiteSpace(existing))
            return existing;

        _ = FetchAndCacheGalBankCodeAsync(userId);
        return string.Empty;
    }

    public void InvalidateGalBankCode(NetUserId userId)
    {
        _galBankCodeByUser.Remove(userId);
    }

    private async Task FetchAndCacheGalBankCodeAsync(NetUserId userId, bool forceRefresh = false)
    {
        if (!_galBankCodeFetchInFlight.Add(userId))
            return;

        try
        {
            var code = await FetchGalBankCodeAsync(userId, forceRefresh);
            if (!string.IsNullOrWhiteSpace(code))
                await RunOnMainThread(() => _galBankCodeByUser[userId] = code);
        }
        finally
        {
            _galBankCodeFetchInFlight.Remove(userId);
        }
    }

    public async Task<string?> FetchGalBankCodeAsync(NetUserId userId, bool forceRefresh = false)
    {
        var url = _cfg.GetCVar(CLVars.TransferApiUrl)?.Trim().TrimEnd('/');
        var secret = _cfg.GetCVar(CLVars.TransferApiSecret);
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(secret))
            return null;

        if (!forceRefresh &&
            _galBankCodeByUser.TryGetValue(userId, out var cached) &&
            !string.IsNullOrWhiteSpace(cached))
            return cached;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{url}/api/galbank/code?userId={userId.UserId}");
            request.Headers.TryAddWithoutValidation("X-Api-Secret", secret);
            using var response = await _galBankHttp.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _log.Warning($"GalBank code fetch failed for {userId}: {response.StatusCode}");
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<GalBankCodeResponse>(GalBankJsonOptions);
            if (payload == null || string.IsNullOrWhiteSpace(payload.CardNumber))
                return null;

            var code = payload.CardNumber.Trim().ToUpperInvariant();
            await RunOnMainThread(() => _galBankCodeByUser[userId] = code);
            return code;
        }
        catch (Exception e)
        {
            _log.Warning($"GalBank code fetch error for {userId}: {e.Message}");
            return null;
        }
    }

    public async Task<(bool ok, GalBankTransferError error, int newBalance, int amount, Guid? toUserId)> TryGalBankTransferAsync(
        EntityUid sender,
        string targetCard,
        int amount)
    {
        if (amount <= 0)
            return (false, GalBankTransferError.InvalidAmount, 0, 0, null);

        var source = GetGalBankRootOwner(sender);
        if (!_playerManager.TryGetSessionByEntity(source, out var senderSession))
            return (false, GalBankTransferError.InvalidAmount, 0, 0, null);

        var userId = senderSession.UserId;
        if (!_galBankTransferInFlight.Add(userId))
            return (false, GalBankTransferError.Cooldown, 0, 0, null);

        if (_galBankLastTransferAt.TryGetValue(userId, out var last) &&
            _timing.CurTime - last < GalBankTransferCooldown)
        {
            _galBankTransferInFlight.Remove(userId);
            return (false, GalBankTransferError.Cooldown, 0, 0, null);
        }

        try
        {
            var url = _cfg.GetCVar(CLVars.TransferApiUrl)?.Trim().TrimEnd('/');
            var secret = _cfg.GetCVar(CLVars.TransferApiSecret);
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(secret))
                return (false, GalBankTransferError.NetworkError, 0, 0, null);

            if (string.IsNullOrWhiteSpace(targetCard))
                return (false, GalBankTransferError.InvalidTarget, 0, 0, null);

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{url}/api/galbank/game-transfer");
            request.Headers.TryAddWithoutValidation("X-Api-Secret", secret);
            request.Content = JsonContent.Create(new GalBankGameTransferRequest
            {
                FromUserId = senderSession.UserId.UserId.ToString(),
                TargetCard = targetCard.Trim(),
                Amount = amount
            });

            using var response = await _galBankHttp.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return (false, MapGalBankTransferError(body), 0, 0, null);

            GalBankGameTransferResponse? payload = null;
            try
            {
                payload = JsonSerializer.Deserialize<GalBankGameTransferResponse>(body, GalBankJsonOptions);
            }
            catch
            {
            }

            if (payload == null || !payload.Success)
                return (false, MapGalBankTransferError(body), 0, 0, null);

            Guid? toUserId = null;
            if (!string.IsNullOrWhiteSpace(payload.ToUserId) && Guid.TryParse(payload.ToUserId, out var parsed) && parsed != Guid.Empty)
                toUserId = parsed;

            var transferred = payload.Amount > 0 ? payload.Amount : amount;
            var newBalance = await RunOnMainThread(() =>
            {
                _galBankLastTransferAt[userId] = _timing.CurTime;
                TryGetBalance(source, out var balance);
                return balance;
            });

            return (true, GalBankTransferError.None, newBalance, transferred, toUserId);
        }
        catch (Exception e)
        {
            _log.Warning($"GalBank transfer network error: {e.Message}");
            return (false, GalBankTransferError.NetworkError, 0, 0, null);
        }
        finally
        {
            await RunOnMainThread(() => _galBankTransferInFlight.Remove(userId));
        }
    }

    private static GalBankTransferError MapGalBankTransferError(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return GalBankTransferError.NetworkError;

        try
        {
            var payload = JsonSerializer.Deserialize<GalBankGameTransferResponse>(body, GalBankJsonOptions);
            return MapGalBankErrorCode(payload?.Error);
        }
        catch
        {
            return GalBankTransferError.NetworkError;
        }
    }

    private static GalBankTransferError MapGalBankErrorCode(string? error)
    {
        return error switch
        {
            "self_transfer" => GalBankTransferError.SelfTransfer,
            "insufficient_funds" => GalBankTransferError.InsufficientFunds,
            "invalid_account" or "account_not_found" or "sender_not_found"
                or "sender_blocked" or "recipient_blocked" => GalBankTransferError.InvalidTarget,
            "invalid_amount" or "invalid_request" => GalBankTransferError.InvalidAmount,
            _ => GalBankTransferError.NetworkError
        };
    }

    private async Task<T> RunOnMainThread<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _taskManager.RunOnMainThread(() =>
        {
            try
            {
                tcs.TrySetResult(func());
            }
            catch (Exception e)
            {
                tcs.TrySetException(e);
            }
        });
        return await tcs.Task;
    }

    private EntityUid GetGalBankRootOwner(EntityUid ent)
    {
        var current = ent;
        while (_containerSystem.TryGetContainingContainer(current, out var cont))
            current = cont.Owner;
        return current;
    }

    private void ClearGalBankCodeCache()
    {
        _galBankCodeByUser.Clear();
        _galBankCodeFetchInFlight.Clear();
        _galBankTransferInFlight.Clear();
        _galBankLastTransferAt.Clear();
    }

    private sealed class GalBankCodeResponse
    {
        [JsonPropertyName("cardNumber")]
        public string? CardNumber { get; set; }
    }

    private sealed class GalBankGameTransferRequest
    {
        [JsonPropertyName("fromUserId")]
        public string FromUserId { get; set; } = string.Empty;

        [JsonPropertyName("targetCard")]
        public string TargetCard { get; set; } = string.Empty;

        [JsonPropertyName("amount")]
        public int Amount { get; set; }
    }

    private sealed class GalBankGameTransferResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("amount")]
        public int Amount { get; set; }

        [JsonPropertyName("toUserId")]
        public string? ToUserId { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
}
