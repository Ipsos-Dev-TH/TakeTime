using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TakeTime.Infrastructure.ExternalServices;

/// <summary>
/// Configuration options for Telegram Bot integration.
/// </summary>
public class TelegramSettings
{
    public const string SectionName = "Telegram";

    /// <summary>
    /// Telegram Bot API token.
    /// </summary>
    public string BotToken { get; set; } = string.Empty;

    /// <summary>
    /// Default chat ID for notifications.
    /// </summary>
    public string DefaultChatId { get; set; } = string.Empty;

    /// <summary>
    /// Telegram Bot API base URL.
    /// </summary>
    public string ApiBaseUrl { get; set; } = "https://api.telegram.org";

    /// <summary>
    /// Whether Telegram notifications are enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Interface for Telegram bot messaging.
/// </summary>
public interface ITelegramService
{
    Task SendMessageAsync(string message, string? chatId = null, CancellationToken cancellationToken = default);
    Task SendMessageWithMarkdownAsync(string markdownMessage, string? chatId = null, CancellationToken cancellationToken = default);
    Task SendDocumentAsync(byte[] document, string fileName, string? caption = null, string? chatId = null, CancellationToken cancellationToken = default);
    Task SendPhotoAsync(string photoUrl, string? caption = null, string? chatId = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Telegram Bot implementation for sending notifications via the Telegram Bot API.
/// Used for operational alerts and guest communication.
/// </summary>
public class TelegramService : ITelegramService
{
    private readonly HttpClient _httpClient;
    private readonly TelegramSettings _settings;
    private readonly ILogger<TelegramService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TelegramService(
        HttpClient httpClient,
        IOptions<TelegramSettings> settings,
        ILogger<TelegramService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task SendMessageAsync(
        string message,
        string? chatId = null,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            _logger.LogDebug("Telegram notifications are disabled. Skipping message.");
            return;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var targetChatId = chatId ?? _settings.DefaultChatId;
        if (string.IsNullOrEmpty(targetChatId))
        {
            _logger.LogWarning("No Telegram chat ID configured. Skipping message.");
            return;
        }

        try
        {
            var url = $"{_settings.ApiBaseUrl}/bot{_settings.BotToken}/sendMessage";

            var payload = new
            {
                chat_id = targetChatId,
                text = message,
                parse_mode = "HTML"
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(url, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Telegram message sent successfully to chat {ChatId}", targetChatId);
            }
            else
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Telegram API returned {StatusCode}: {ResponseBody}",
                    response.StatusCode,
                    responseBody);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram message to chat {ChatId}", targetChatId);
        }
    }

    /// <inheritdoc />
    public async Task SendMessageWithMarkdownAsync(
        string markdownMessage,
        string? chatId = null,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled) return;

        ArgumentException.ThrowIfNullOrWhiteSpace(markdownMessage);

        var targetChatId = chatId ?? _settings.DefaultChatId;
        if (string.IsNullOrEmpty(targetChatId)) return;

        try
        {
            var url = $"{_settings.ApiBaseUrl}/bot{_settings.BotToken}/sendMessage";

            var payload = new
            {
                chat_id = targetChatId,
                text = markdownMessage,
                parse_mode = "MarkdownV2"
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Telegram markdown message failed: {StatusCode} {Body}",
                    response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram markdown message");
        }
    }

    /// <inheritdoc />
    public async Task SendDocumentAsync(
        byte[] document,
        string fileName,
        string? caption = null,
        string? chatId = null,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled) return;

        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var targetChatId = chatId ?? _settings.DefaultChatId;
        if (string.IsNullOrEmpty(targetChatId)) return;

        try
        {
            var url = $"{_settings.ApiBaseUrl}/bot{_settings.BotToken}/sendDocument";

            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(targetChatId), "chat_id");

            var fileContent = new ByteArrayContent(document);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            form.Add(fileContent, "document", fileName);

            if (!string.IsNullOrEmpty(caption))
            {
                form.Add(new StringContent(caption), "caption");
            }

            var response = await _httpClient.PostAsync(url, form, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Telegram document send failed: {StatusCode} {Body}",
                    response.StatusCode, body);
            }
            else
            {
                _logger.LogInformation("Telegram document '{FileName}' sent to chat {ChatId}",
                    fileName, targetChatId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram document '{FileName}'", fileName);
        }
    }

    /// <inheritdoc />
    public async Task SendPhotoAsync(
        string photoUrl,
        string? caption = null,
        string? chatId = null,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled) return;

        ArgumentException.ThrowIfNullOrWhiteSpace(photoUrl);

        var targetChatId = chatId ?? _settings.DefaultChatId;
        if (string.IsNullOrEmpty(targetChatId)) return;

        try
        {
            var url = $"{_settings.ApiBaseUrl}/bot{_settings.BotToken}/sendPhoto";

            var payload = new
            {
                chat_id = targetChatId,
                photo = photoUrl,
                caption = caption ?? string.Empty
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Telegram photo send failed: {StatusCode} {Body}",
                    response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram photo");
        }
    }
}
