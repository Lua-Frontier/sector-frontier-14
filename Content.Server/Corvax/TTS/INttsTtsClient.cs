using System.Threading;
using System.Threading.Tasks;

namespace Content.Server.Corvax.TTS;

public interface INttsTtsClient
{
    bool IsNttsUrl(string apiUrl);

    Task<byte[]?> SynthesizeAsync(
        string apiUrl,
        string apiToken,
        string speaker,
        string text,
        string ext,
        string? effect,
        CancellationToken cancel);

    Task<IReadOnlyList<string>?> GetEffectsAsync(
        string apiUrl,
        string apiToken,
        CancellationToken cancel);
}

public sealed class NullNttsTtsClient : INttsTtsClient
{
    public bool IsNttsUrl(string apiUrl) => false;

    public Task<byte[]?> SynthesizeAsync(
        string apiUrl,
        string apiToken,
        string speaker,
        string text,
        string ext,
        string? effect,
        CancellationToken cancel) => Task.FromResult<byte[]?>(null);

    public Task<IReadOnlyList<string>?> GetEffectsAsync(
        string apiUrl,
        string apiToken,
        CancellationToken cancel) => Task.FromResult<IReadOnlyList<string>?>(null);
}

public readonly record struct TtsAudioResult(byte[] Data, string Format);
