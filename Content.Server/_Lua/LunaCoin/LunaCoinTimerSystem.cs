// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared.Lua.CLVar;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Utility;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Content.Server._Lua.LunaCoin;

public sealed class LunaCoinTimerSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private readonly HttpClient _httpClient = new();
    private ISawmill _sawmill = default!;

    private string _apiUrl = string.Empty;
    private string _apiToken = string.Empty;
    private string _serverName = string.Empty;

    private float _elapsed;
    private const float TimerInterval = 60f;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("lunacoin");
        _cfg.OnValueChanged(CLVars.LunaCoinApiUrl, v => _apiUrl = v, true);
        _cfg.OnValueChanged(CLVars.LunaCoinApiToken, v => _apiToken = v, true);
        _cfg.OnValueChanged(CLVars.LunaCoinServerName, v => _serverName = v, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _elapsed += frameTime;
        if (_elapsed < TimerInterval) return;
        _elapsed = 0f;
        SendTimerAsync();
    }

    private async void SendTimerAsync()
    {
        if (string.IsNullOrEmpty(_apiUrl) || string.IsNullOrEmpty(_apiToken)) return;
        if (string.IsNullOrWhiteSpace(_serverName))
        {
            _sawmill.Warning("Timer skipped: LunaCoinServerName is not configured");
            return;
        }

        var players = new List<Guid>();
        foreach (var session in _playerManager.Sessions)
        {
            players.Add(session.UserId.UserId);
        }

        if (players.Count == 0) return;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_apiUrl}/api/lunacoin/timer");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);
            request.Content = JsonContent.Create(new TimerRequest(_serverName, players));
            using var response = await _httpClient.SendAsync(request, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cts.Token);
                _sawmill.Warning($"Timer failed: {response.StatusCode} - {body}");
            }
        }
        catch (TaskCanceledException e)
        {
            _sawmill.Warning($"Timer timeout: {e.Message}");
        }
        catch (HttpRequestException e)
        {
            _sawmill.Warning($"Timer network error: {e.Message}");
        }
        catch (Exception e)
        {
            _sawmill.Error($"Timer exception: {e}");
        }
    }

    private sealed record TimerRequest(string Server, List<Guid> Players);
}