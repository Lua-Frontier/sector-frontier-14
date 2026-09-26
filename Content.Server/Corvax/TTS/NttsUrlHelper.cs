using System;

namespace Content.Server.Corvax.TTS;

public static class NttsUrlHelper
{
    public static bool IsNttsUrl(string? apiUrl)
    {
        if (string.IsNullOrWhiteSpace(apiUrl))
            return false;

        if (!Uri.TryCreate(apiUrl, UriKind.Absolute, out var uri))
            return apiUrl.Contains("/api/v1/tts", StringComparison.OrdinalIgnoreCase);

        if (uri.Host.Contains("ntts.fdev.team", StringComparison.OrdinalIgnoreCase))
            return true;

        return uri.AbsolutePath.Contains("/api/v1/tts", StringComparison.OrdinalIgnoreCase);
    }

    public static string GetBaseUrl(string apiUrl)
    {
        if (Uri.TryCreate(apiUrl, UriKind.Absolute, out var uri))
            return $"{uri.Scheme}://{uri.Authority}";

        return apiUrl.TrimEnd('/');
    }

    public static string SynthesizeUrl(string apiUrl)
    {
        if (Uri.TryCreate(apiUrl, UriKind.Absolute, out var uri) &&
            uri.AbsolutePath.Contains("/api/v1/tts", StringComparison.OrdinalIgnoreCase) &&
            !uri.AbsolutePath.Contains("/speakers", StringComparison.OrdinalIgnoreCase) &&
            !uri.AbsolutePath.Contains("/effects", StringComparison.OrdinalIgnoreCase))
        {
            return $"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath.TrimEnd('/')}";
        }

        return $"{GetBaseUrl(apiUrl).TrimEnd('/')}/api/v1/tts";
    }

    public static string EffectsUrl(string apiUrl) =>
        $"{GetBaseUrl(apiUrl).TrimEnd('/')}/api/v1/tts/effects";
}
