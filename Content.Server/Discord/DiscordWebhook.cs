using System.Net.Http;
using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Content.Server.Discord;

public sealed class DiscordWebhook : IPostInjectInit
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan BaseRetryDelay = TimeSpan.FromSeconds(2);
    private const int MaxAttempts = 4;

    [Dependency] private readonly ILogManager _log = default!;

    private const string BaseUrl = "https://discord.com/api/v10/webhooks";
    private readonly HttpClient _http = new() { Timeout = RequestTimeout };
    private ISawmill _sawmill = default!;

    private string GetUrl(WebhookIdentifier identifier)
    {
        return $"{BaseUrl}/{identifier.Id}/{identifier.Token}";
    }

    /// <summary>
    ///     Gets the webhook data from the given webhook url.
    /// </summary>
    /// <param name="url">The url to get the data from.</param>
    /// <returns>The webhook data returned from the url.</returns>
    public async Task<WebhookData?> GetWebhook(string url)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var response = await _http.GetAsync(url);

                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<WebhookData>();

                LogResponse(response, "Get");

                if (!ShouldRetry(response, attempt))
                    return null;

                await DelayBeforeRetry(response, attempt);
            }
            catch (Exception e) when (IsTransientException(e) && attempt < MaxAttempts)
            {
                _sawmill.Warning($"Transient error getting discord webhook data on attempt {attempt}/{MaxAttempts}: {e.Message}");
                await DelayBeforeRetry(null, attempt);
            }
            catch (Exception e)
            {
                _sawmill.Error($"Error getting discord webhook data.\n{e}");
                return null;
            }
        }

        return null;
    }

    /// <summary>
    ///     Gets the webhook data from the given webhook url.
    /// </summary>
    /// <param name="url">The url to get the data from.</param>
    /// <param name="onComplete">The delegate to invoke with the obtained data, if any.</param>
    public async void GetWebhook(string url, Action<WebhookData> onComplete)
    {
        if (await GetWebhook(url) is { } data)
            onComplete(data);
    }

    /// <summary>
    ///     Tries to get the webhook data from the given webhook url if it is not null or whitespace.
    /// </summary>
    /// <param name="url">The url to get the data from.</param>
    /// <param name="onComplete">The delegate to invoke with the obtained data, if any.</param>
    public async void TryGetWebhook(string url, Action<WebhookData> onComplete)
    {
        if (await GetWebhook(url) is { } data)
            onComplete(data);
    }

    /// <summary>
    ///     Creates a new webhook message with the given identifier and payload.
    /// </summary>
    /// <param name="identifier">The identifier for the webhook url.</param>
    /// <param name="payload">The payload to create the message from.</param>
    /// <returns>The response from Discord's API.</returns>
    public async Task<HttpResponseMessage> CreateMessage(WebhookIdentifier identifier, WebhookPayload payload)
    {
        var url = $"{GetUrl(identifier)}?wait=true";
        return await SendWithRetry(
            ct => _http.PostAsJsonAsync(url, payload, JsonOptions, ct),
            "Create");
    }

    /// <summary>
    ///     Deletes a webhook message with the given identifier and message id.
    /// </summary>
    /// <param name="identifier">The identifier for the webhook url.</param>
    /// <param name="messageId">The message id to delete.</param>
    /// <returns>The response from Discord's API.</returns>
    public async Task<HttpResponseMessage> DeleteMessage(WebhookIdentifier identifier, ulong messageId)
    {
        var url = $"{GetUrl(identifier)}/messages/{messageId}";
        return await SendWithRetry(
            ct => _http.DeleteAsync(url, ct),
            "Delete");
    }

    /// <summary>
    ///     Creates a new webhook message with the given identifier, message id and payload.
    /// </summary>
    /// <param name="identifier">The identifier for the webhook url.</param>
    /// <param name="messageId">The message id to edit.</param>
    /// <param name="payload">The payload used to edit the message.</param>
    /// <returns>The response from Discord's API.</returns>
    public async Task<HttpResponseMessage> EditMessage(WebhookIdentifier identifier, ulong messageId, WebhookPayload payload)
    {
        var url = $"{GetUrl(identifier)}/messages/{messageId}";
        return await SendWithRetry(
            ct => _http.PatchAsJsonAsync(url, payload, JsonOptions, ct),
            "Edit");
    }

    void IPostInjectInit.PostInject()
    {
        _sawmill = _log.GetSawmill("DISCORD");
    }

    /// <summary>
    ///     Logs detailed information about the HTTP response received from a Discord webhook request.
    ///     If the response status code is non-2XX it logs the status code, relevant rate limit headers.
    /// </summary>
    /// <param name="response">The HTTP response received from the Discord API.</param>
    /// <param name="methodName">The name (constant) of the method that initiated the webhook request (e.g., "Create", "Edit", "Delete").</param>
    private void LogResponse(HttpResponseMessage response, string methodName)
    {
        if (!response.IsSuccessStatusCode)
        {
            _sawmill.Error($"Failed to {methodName} message. Status code: {response.StatusCode}.");

            if (response.Headers.TryGetValues("Retry-After", out var retryAfter))
                _sawmill.Debug($"Failed webhook response Retry-After: {string.Join(", ", retryAfter)}");

            if (response.Headers.TryGetValues("X-RateLimit-Global", out var globalRateLimit))
                _sawmill.Debug($"Failed webhook response X-RateLimit-Global: {string.Join(", ", globalRateLimit)}");

            if (response.Headers.TryGetValues("X-RateLimit-Scope", out var rateLimitScope))
                _sawmill.Debug($"Failed webhook response X-RateLimit-Scope: {string.Join(", ", rateLimitScope)}");
        }
    }

    private async Task<HttpResponseMessage> SendWithRetry(
        Func<CancellationToken, Task<HttpResponseMessage>> sendRequest,
        string methodName,
        CancellationToken cancellationToken = default)
    {
        HttpResponseMessage? response = null;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                response = await sendRequest(cancellationToken);

                if (response.IsSuccessStatusCode || !ShouldRetry(response, attempt))
                {
                    LogResponse(response, methodName);
                    return response;
                }

                LogRetry(methodName, attempt, response.StatusCode, null);
                response.Dispose();
                response = null;

                await DelayBeforeRetry(response, attempt, cancellationToken);
            }
            catch (Exception e) when (IsTransientException(e) && attempt < MaxAttempts)
            {
                LogRetry(methodName, attempt, null, e);
                await DelayBeforeRetry(null, attempt, cancellationToken);
            }
        }

        throw new HttpRequestException($"Discord webhook {methodName} request failed after {MaxAttempts} attempts.");
    }

    private bool ShouldRetry(HttpResponseMessage response, int attempt)
    {
        if (attempt >= MaxAttempts)
            return false;

        var statusCode = (int) response.StatusCode;
        return statusCode == 408 || statusCode == 429 || statusCode >= 500;
    }

    private bool IsTransientException(Exception exception)
    {
        return exception is HttpRequestException or TaskCanceledException;
    }

    private async Task DelayBeforeRetry(
        HttpResponseMessage? response,
        int attempt,
        CancellationToken cancellationToken = default)
    {
        var delay = GetRetryDelay(response, attempt);
        await Task.Delay(delay, cancellationToken);
    }

    private TimeSpan GetRetryDelay(HttpResponseMessage? response, int attempt)
    {
        if (response?.Headers.RetryAfter?.Delta is { } retryAfterDelta)
            return retryAfterDelta;

        if (response != null && response.Headers.TryGetValues("Retry-After", out var retryAfterValues))
        {
            foreach (var value in retryAfterValues)
            {
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
                    return TimeSpan.FromSeconds(Math.Max(seconds, 0));
            }
        }

        return TimeSpan.FromSeconds(BaseRetryDelay.TotalSeconds * attempt);
    }

    private void LogRetry(string methodName, int attempt, System.Net.HttpStatusCode? statusCode, Exception? exception)
    {
        var reason = statusCode != null
            ? $"status {(int) statusCode} ({statusCode})"
            : exception?.GetType().Name ?? "unknown error";

        _sawmill.Warning($"Retrying Discord webhook {methodName} request after transient failure on attempt {attempt}/{MaxAttempts}: {reason}.");
    }


}
