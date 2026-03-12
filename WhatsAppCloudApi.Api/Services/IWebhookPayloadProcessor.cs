using System.Text.Json;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.Models;

namespace WhatsAppCloudApi.Api.Services;

public interface IWebhookPayloadProcessor
{
    Task ProcessAsync(string payloadJson, int companyId, int whatsAppPhoneNumberId, CancellationToken cancellationToken = default);
}

public sealed class WebhookPayloadProcessor : IWebhookPayloadProcessor
{
    private readonly IConversationService _conversationService;
    private readonly ILogger<WebhookPayloadProcessor> _logger;

    public WebhookPayloadProcessor(IConversationService conversationService, ILogger<WebhookPayloadProcessor> logger)
    {
        _conversationService = conversationService;
        _logger = logger;
    }

    public async Task ProcessAsync(string payloadJson, int companyId, int whatsAppPhoneNumberId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(payloadJson) || companyId <= 0 || whatsAppPhoneNumberId <= 0)
        {
            return;
        }

        WebhookPayload? webhook;
        try
        {
            webhook = JsonSerializer.Deserialize<WebhookPayload>(payloadJson);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize webhook payload for company {CompanyId}.", companyId);
            return;
        }

        if (webhook is null)
        {
            return;
        }

        foreach (var entry in webhook.Entry)
        {
            foreach (var change in entry.Changes)
            {
                var value = change.Value;
                if (value is null)
                {
                    continue;
                }

                if (value.Messages is { Count: > 0 })
                {
                    foreach (var message in value.Messages)
                    {
                        await ProcessInboundMessageAsync(message, value, companyId, whatsAppPhoneNumberId, cancellationToken);
                    }
                }

                if (value.Statuses is { Count: > 0 })
                {
                    foreach (var status in value.Statuses)
                    {
                        await ProcessStatusUpdateAsync(status, cancellationToken);
                    }
                }
            }
        }
    }

    private async Task ProcessInboundMessageAsync(
        WebhookMessage message,
        WebhookValue value,
        int companyId,
        int whatsAppPhoneNumberId,
        CancellationToken cancellationToken)
    {
        var messageType = message.Type ?? "text";
        string content;
        string? mediaUrl = null;
        string? mediaMimeType = null;
        string? fileName = null;
        string? interactiveReplyId = null;
        string? interactiveReplyTitle = null;

        if (string.Equals(messageType, "text", StringComparison.OrdinalIgnoreCase))
        {
            content = message.Text?.Body ?? string.Empty;
        }
        else if (string.Equals(messageType, "button", StringComparison.OrdinalIgnoreCase))
        {
            interactiveReplyId = message.Button?.Payload;
            interactiveReplyTitle = message.Button?.Text;
            content = interactiveReplyTitle ?? interactiveReplyId ?? string.Empty;
        }
        else if (string.Equals(messageType, "interactive", StringComparison.OrdinalIgnoreCase))
        {
            interactiveReplyId = message.Interactive?.ButtonReply?.Id ?? message.Interactive?.ListReply?.Id;
            interactiveReplyTitle = message.Interactive?.ButtonReply?.Title ?? message.Interactive?.ListReply?.Title;
            content = interactiveReplyTitle ?? interactiveReplyId ?? string.Empty;
        }
        else
        {
            var media = message.Image ?? message.Video ?? message.Audio ?? message.Document ?? message.Sticker;
            mediaUrl = media?.Id;
            mediaMimeType = media?.MimeType;
            fileName = media?.FileName;
            content = media?.Caption ?? string.Empty;
        }

        var contactName = value.Contacts?.FirstOrDefault(x => x.WaId == message.From)?.Profile?.Name;

        try
        {
            var occurredAtUtc = TryParseWebhookTimestampUtc(message.Timestamp);
            await _conversationService.ProcessInboundMessageAsync(
                companyId,
                message.From ?? string.Empty,
                contactName,
                whatsAppPhoneNumberId,
                message.Id ?? string.Empty,
                messageType,
                content,
                mediaUrl,
                mediaMimeType,
                fileName,
                interactiveReplyId,
                interactiveReplyTitle,
                occurredAtUtc,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist inbound message {MetaId} for company {CompanyId}.", message.Id, companyId);
        }
    }

    private async Task ProcessStatusUpdateAsync(WebhookStatus status, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(status.Id) || string.IsNullOrWhiteSpace(status.Status))
        {
            return;
        }

        try
        {
            await _conversationService.ProcessStatusUpdateAsync(status.Id, status.Status, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process status update for message {MetaId}.", status.Id);
        }
    }

    private static DateTime? TryParseWebhookTimestampUtc(string? timestamp)
    {
        if (string.IsNullOrWhiteSpace(timestamp) || !long.TryParse(timestamp, out var raw))
        {
            return null;
        }

        try
        {
            // Meta webhook timestamps are normally Unix seconds; support milliseconds defensively.
            return timestamp.Length >= 13
                ? DateTimeOffset.FromUnixTimeMilliseconds(raw).UtcDateTime
                : DateTimeOffset.FromUnixTimeSeconds(raw).UtcDateTime;
        }
        catch
        {
            return null;
        }
    }
}
