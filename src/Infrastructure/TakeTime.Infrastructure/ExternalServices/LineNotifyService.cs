using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TakeTime.Infrastructure.ExternalServices;

/// <summary>
/// Configuration options for LINE Notify integration.
/// </summary>
public class LineNotifySettings
{
    public const string SectionName = "LineNotify";

    /// <summary>
    /// LINE Notify API endpoint.
    /// </summary>
    public string ApiUrl { get; set; } = "https://notify-api.line.me/api/notify";

    /// <summary>
    /// Default access token for LINE Notify. Can be overridden per-tenant.
    /// </summary>
    public string DefaultAccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Whether LINE Notify is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Interface for LINE Notify messaging.
/// </summary>
public interface ILineNotifyService
{
    Task SendNotificationAsync(string message, string? accessToken = null, CancellationToken cancellationToken = default);
    Task SendNotificationWithImageAsync(string message, string imageUrl, string? accessToken = null, CancellationToken cancellationToken = default);
    Task SendNotificationWithStickerAsync(string message, int stickerPackageId, int stickerId, string? accessToken = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// LINE Notify implementation for sending notifications to LINE groups and users.
/// Used for real-time operational alerts such as new bookings, check-ins, and issues.
/// </summary>
public class LineNotifyService : ILineNotifyService
{
    private readonly HttpClient _httpClient;
    private readonly LineNotifySettings _settings;
    private readonly ILogger<LineNotifyService> _logger;

    public LineNotifyService(
        HttpClient httpClient,
        IOptions<LineNotifySettings> settings,
        ILogger<LineNotifyService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task SendNotificationAsync(
        string message,
        string? accessToken = null,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            _logger.LogDebug("LINE Notify is disabled. Skipping notification.");
            return;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        // LINE Notify has a 1000 character limit
        if (message.Length > 1000)
        {
            message = message[..997] + "...";
        }

        var token = accessToken ?? _settings.DefaultAccessToken;
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("No LINE Notify access token configured. Skipping notification.");
            return;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _settings.ApiUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("message", message)
            });

            request.Content = formData;

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("LINE Notify message sent successfully");
            }
            else
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "LINE Notify API returned {StatusCode}: {ResponseBody}",
                    response.StatusCode,
                    responseBody);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send LINE Notify message");
        }
    }

    /// <inheritdoc />
    public async Task SendNotificationWithImageAsync(
        string message,
        string imageUrl,
        string? accessToken = null,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled) return;

        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(imageUrl);

        var token = accessToken ?? _settings.DefaultAccessToken;
        if (string.IsNullOrEmpty(token)) return;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _settings.ApiUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("message", message),
                new KeyValuePair<string, string>("imageThumbnail", imageUrl),
                new KeyValuePair<string, string>("imageFullsize", imageUrl)
            });

            request.Content = formData;

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("LINE Notify image notification failed: {StatusCode} {Body}",
                    response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send LINE Notify image notification");
        }
    }

    /// <inheritdoc />
    public async Task SendNotificationWithStickerAsync(
        string message,
        int stickerPackageId,
        int stickerId,
        string? accessToken = null,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled) return;

        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var token = accessToken ?? _settings.DefaultAccessToken;
        if (string.IsNullOrEmpty(token)) return;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _settings.ApiUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("message", message),
                new KeyValuePair<string, string>("stickerPackageId", stickerPackageId.ToString()),
                new KeyValuePair<string, string>("stickerId", stickerId.ToString())
            });

            request.Content = formData;

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("LINE Notify sticker notification failed: {StatusCode} {Body}",
                    response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send LINE Notify sticker notification");
        }
    }
}
