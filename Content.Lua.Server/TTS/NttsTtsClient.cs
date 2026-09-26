// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Corvax.TTS;

namespace Content.Lua.Server.TTS;

public sealed class NttsTtsClient : INttsTtsClient
{
    private static readonly Regex SsmlTagRegex = new(@"</?[^>]+>", RegexOptions.Compiled);

    private readonly HttpClient _http = new();
    private readonly ISawmill _sawmill;

    public NttsTtsClient()
    {
        _sawmill = Logger.GetSawmill("tts.ntts");
    }

    public bool IsNttsUrl(string apiUrl) => NttsUrlHelper.IsNttsUrl(apiUrl);

    public async Task<byte[]?> SynthesizeAsync(
        string apiUrl,
        string apiToken,
        string speaker,
        string text,
        string ext,
        string? effect,
        CancellationToken cancel)
    {
        var plain = StripSsml(text);
        if (string.IsNullOrWhiteSpace(plain))
            return null;

        var url = NttsUrlHelper.SynthesizeUrl(apiUrl);
        var query = new List<string>
        {
            $"speaker={Uri.EscapeDataString(speaker)}",
            $"text={Uri.EscapeDataString(plain)}",
            $"ext={Uri.EscapeDataString(ext)}",
        };
        if (!string.IsNullOrWhiteSpace(effect))
            query.Add($"effect={Uri.EscapeDataString(effect)}");

        var requestUri = $"{url}?{string.Join('&', query)}";
        using var request = CreateRequest(HttpMethod.Get, requestUri, apiToken);
        using var response = await _http.SendAsync(request, cancel);
        if (!response.IsSuccessStatusCode)
        {
            _sawmill.Error($"NTTS synthesize failed: {(int) response.StatusCode} {response.ReasonPhrase}");
            return null;
        }

        return await response.Content.ReadAsByteArrayAsync(cancel);
    }

    public async Task<IReadOnlyList<string>?> GetEffectsAsync(
        string apiUrl,
        string apiToken,
        CancellationToken cancel)
    {
        using var request = CreateRequest(HttpMethod.Get, NttsUrlHelper.EffectsUrl(apiUrl), apiToken);
        using var response = await _http.SendAsync(request, cancel);
        if (!response.IsSuccessStatusCode)
        {
            _sawmill.Error($"NTTS effects list failed: {(int) response.StatusCode} {response.ReasonPhrase}");
            return null;
        }

        var dto = await response.Content.ReadFromJsonAsync<EffectsDto>(cancellationToken: cancel);
        return dto?.Effects;
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string uri, string apiToken)
    {
        var request = new HttpRequestMessage(method, uri);
        if (!string.IsNullOrWhiteSpace(apiToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        return request;
    }

    private static string StripSsml(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return SsmlTagRegex.Replace(text, string.Empty).Trim();
    }

    private sealed class EffectsDto
    {
        [JsonPropertyName("effects")]
        public List<string>? Effects { get; set; }
    }
}
