// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Content.Lua.Common.CLVar;
using Content.Lua.Common.SitePlayerSync;
using Robust.Shared.Configuration;
using Robust.Shared.Network;

namespace Content.Lua.Server.SitePlayerSync;

public sealed class SitePlayerSyncManager : ISitePlayerSyncManager
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly ILogManager _logManager = default!;

    private readonly HttpClient _http = new();
    private ISawmill _sawmill = default!;

    private string _apiUrl = string.Empty;
    private string _apiToken = string.Empty;
    private bool _enabled;

    public void Initialize()
    {
        _sawmill = _logManager.GetSawmill("siteplayersync");
        _cfg.OnValueChanged(CLVars.SitePlayerSyncApiUrl, v => _apiUrl = v?.Trim() ?? string.Empty, true);
        _cfg.OnValueChanged(CLVars.SitePlayerSyncApiToken, v => _apiToken = v?.Trim() ?? string.Empty, true);
        _cfg.OnValueChanged(CLVars.SitePlayerSyncEnabled, v => _enabled = v, true);
    }

    public void NotifyPlayerSeen(NetUserId userId, string userName)
    {
        if (!_enabled)
            return;
        if (string.IsNullOrWhiteSpace(_apiUrl) || string.IsNullOrWhiteSpace(_apiToken))
            return;
        if (string.IsNullOrWhiteSpace(userName))
            return;

        _ = NotifyAsync(userId.UserId, userName);
    }

    private async Task NotifyAsync(Guid userId, string userName)
    {
        try
        {
            var url = $"{_apiUrl.TrimEnd('/')}/api/player/seen";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);
            request.Content = JsonContent.Create(new PlayerSeenRequest(userId, userName));
            using var response = await _http.SendAsync(request, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cts.Token);
                _sawmill.Warning($"Player seen notify failed: {response.StatusCode} - {body}");
            }
        }
        catch (TaskCanceledException e)
        {
            _sawmill.Debug($"Player seen notify timeout: {e.Message}");
        }
        catch (HttpRequestException e)
        {
            _sawmill.Debug($"Player seen notify network error: {e.Message}");
        }
        catch (Exception e)
        {
            _sawmill.Warning($"Player seen notify exception: {e}");
        }
    }

    private sealed record PlayerSeenRequest(Guid UserId, string UserName);
}
