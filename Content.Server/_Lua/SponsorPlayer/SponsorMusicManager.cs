// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Lua.SponsorPlayer;
using Content.Shared.Lua.CLVar;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Content.Server._Lua.SponsorPlayer;

public sealed class SponsorMusicManager
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IResourceManager _res = default!;
    private readonly HttpClient _httpClient = new();
    private readonly object _cacheLock = new();
    private ISawmill _sawmill = default!;
    private string _apiUrl = string.Empty;
    private string _apiToken = string.Empty;
    private readonly Dictionary<string, CachedTrack> _trackCache = new();
    private static readonly ResPath TrackCacheDir = new ResPath("/SponsorMusic").ToRootedPath();
    private static readonly TimeSpan TrackTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan TrackRetryDelay = TimeSpan.FromMilliseconds(60);
    private static readonly TimeSpan TrackCacheLifetime = TimeSpan.FromMinutes(10);
    private const int TrackCacheEntryLimit = 6;

    public void Initialize()
    {
        _sawmill = Logger.GetSawmill("sponsor_music");
        _cfg.OnValueChanged(CLVars.SponsorMusicApiUrl, v => _apiUrl = v, true);
        _cfg.OnValueChanged(CLVars.SponsorMusicApiToken, v => _apiToken = v, true);
    }

    public async Task<List<SponsorTrackInfo>?> FetchTrackList(string userId)
    {
        if (string.IsNullOrEmpty(_apiUrl)) return null;
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                using var request = CreateRequest(HttpMethod.Get, $"{_apiUrl}/api/game/sponsor-music/{userId}/tracks");
                using var response = await _httpClient.SendAsync(request, cts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cts.Token);
                    _sawmill.Warning($"FetchTrackList failed for {userId}: {response.StatusCode} — {body}");
                    return null;
                }
                var json = await response.Content.ReadFromJsonAsync<TrackListResponse>(cancellationToken: cts.Token);
                return json?.Tracks.Select(t => new SponsorTrackInfo(t.Id, t.Title, t.Hash, t.DurationSeconds)).ToList();
            }
            catch (TaskCanceledException e) when (attempt < 2)
            {
                _sawmill.Warning($"FetchTrackList timeout for {userId}, retrying: {e.Message}");
                await Task.Delay(TrackRetryDelay);
            }
            catch (HttpRequestException e) when (attempt < 2)
            {
                _sawmill.Warning($"FetchTrackList network error for {userId}, retrying: {e.Message}");
                await Task.Delay(TrackRetryDelay);
            }
            catch (Exception e)
            {
                _sawmill.Error($"FetchTrackList exception for {userId}: {e}");
                return null;
            }
        }
        _sawmill.Error($"FetchTrackList failed for {userId} after retry.");
        return null;
    }

    public async Task<byte[]?> FetchTrack(string userId, string trackId, string? trackHash = null)
    {
        if (string.IsNullOrEmpty(_apiUrl)) return null;
        if (TryGetCachedTrack(trackHash, userId, trackId, out var cached)) return cached;
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                using var cts = new CancellationTokenSource(TrackTimeout);
                using var request = CreateRequest(HttpMethod.Get, $"{_apiUrl}/api/game/sponsor-music/{userId}/track/{trackId}");
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    _sawmill.Warning($"FetchTrack failed for {userId}/{trackId}: {response.StatusCode}");
                    return null;
                }
                await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory, cts.Token);
                var data = memory.ToArray();
                var resolvedHash = ResolveTrackHash(trackHash, data);
                CacheTrack(resolvedHash, userId, trackId, data);
                _sawmill.Debug($"FetchTrack OK for {userId}/{trackId} ({resolvedHash}): {data.Length} bytes");
                return data;
            }
            catch (TaskCanceledException e) when (attempt < 2)
            {
                _sawmill.Warning($"FetchTrack timeout for {userId}/{trackId}, retrying: {e.Message}");
                await Task.Delay(TrackRetryDelay);
            }
            catch (HttpRequestException e) when (attempt < 2)
            {
                _sawmill.Warning($"FetchTrack network error for {userId}/{trackId}, retrying: {e.Message}");
                await Task.Delay(TrackRetryDelay);
            }
            catch (Exception e)
            {
                _sawmill.Error($"FetchTrack exception for {userId}/{trackId}: {e}");
                return null;
            }
        }
        _sawmill.Error($"FetchTrack failed for {userId}/{trackId} after retry.");
        return null;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);
        return request;
    }

    private bool TryGetCachedTrack(string? trackHash, string userId, string trackId, out byte[]? data)
    {
        var key = GetCacheKey(trackHash, userId, trackId);

        lock (_cacheLock)
        {
            PruneTrackCache();
            if (_trackCache.TryGetValue(key, out var cached))
            {
                data = cached.Data;
                return true;
            }
        }

        if (trackHash != null && TryReadTrackFromDiskCache(trackHash, out data))
        {
            if (data == null) return false;
            CacheTrack(trackHash, userId, trackId, data);
            return true;
        }
        data = null;
        return false;
    }

    private void CacheTrack(string hash, string userId, string trackId, byte[] data)
    {
        WriteTrackToDiskCache(hash, data);
        lock (_cacheLock)
        {
            var key = GetCacheKey(hash, userId, trackId);
            _trackCache[key] = new CachedTrack(data, DateTime.UtcNow, DateTime.UtcNow + TrackCacheLifetime);
            PruneTrackCache();
        }
    }

    private string ResolveTrackHash(string? expectedHash, byte[] data)
    {
        var hash = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(expectedHash) && !string.Equals(expectedHash, hash, StringComparison.OrdinalIgnoreCase))
        { _sawmill.Warning($"Sponsor track hash mismatch. Expected {expectedHash}, got {hash}."); }
        return hash;
    }

    private string GetCacheKey(string? trackHash, string userId, string trackId)
    { return !string.IsNullOrWhiteSpace(trackHash) ? trackHash : $"{userId}/{trackId}"; }

    private bool TryReadTrackFromDiskCache(string hash, out byte[]? data)
    {
        var path = GetDiskCachePath(hash);
        if (!_res.UserData.Exists(path))
        {
            data = null;
            return false;
        }
        using var stream = _res.UserData.OpenRead(path);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        data = memory.ToArray();
        return true;
    }

    private void WriteTrackToDiskCache(string hash, byte[] data)
    {
        var path = GetDiskCachePath(hash);
        if (_res.UserData.Exists(path)) return;
        try
        {
            _res.UserData.CreateDir(TrackCacheDir);
            using var stream = _res.UserData.OpenWrite(path);
            stream.Write(data);
        }
        catch (IOException) when (_res.UserData.Exists(path))
        {
        }
    }

    private static ResPath GetDiskCachePath(string hash)
    { return (TrackCacheDir / $"{hash}.ogg").ToRootedPath(); }

    private void PruneTrackCache()
    {
        var now = DateTime.UtcNow;
        foreach (var key in _trackCache.Where(pair => pair.Value.ExpiresAt <= now).Select(pair => pair.Key).ToList())
        { _trackCache.Remove(key); }
        if (_trackCache.Count <= TrackCacheEntryLimit) return;
        foreach (var key in _trackCache.OrderBy(pair => pair.Value.CachedAt).Take(_trackCache.Count - TrackCacheEntryLimit).Select(pair => pair.Key).ToList())
        { _trackCache.Remove(key); }
    }
    private sealed class TrackListResponse
    {
        [JsonPropertyName("tracks")]
        public List<TrackDto> Tracks { get; set; } = new();
    }

    private sealed class TrackDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("hash")]
        public string Hash { get; set; } = "";

        [JsonPropertyName("duration_sec")]
        public int DurationSeconds { get; set; }
    }

    private sealed record CachedTrack(byte[] Data, DateTime CachedAt, DateTime ExpiresAt);
}
