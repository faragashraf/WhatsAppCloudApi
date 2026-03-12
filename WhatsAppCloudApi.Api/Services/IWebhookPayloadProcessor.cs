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
        var messageType = string.IsNullOrWhiteSpace(message.Type)
            ? "text"
            : message.Type.Trim().ToLowerInvariant();

        string content = string.Empty;
        string? mediaUrl = null;
        string? mediaMimeType = null;
        string? fileName = null;
        string? interactiveReplyId = null;
        string? interactiveReplyTitle = null;
        string? interactiveType = null;
        string? interactivePayloadJson = null;

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
            interactiveType = message.Interactive?.Type;
            if (string.Equals(interactiveType, "nfm_reply", StringComparison.OrdinalIgnoreCase))
            {
                interactiveReplyId = "nfm_reply";
                interactiveReplyTitle = message.Interactive?.NfmReply?.Name ?? "flow";
                interactivePayloadJson = message.Interactive?.NfmReply?.ResponseJson;

                if (string.IsNullOrWhiteSpace(interactivePayloadJson))
                {
                    interactivePayloadJson = message.Interactive?.NfmReply?.Body;
                }

                content = interactivePayloadJson ?? string.Empty;
            }
            else
            {
                interactiveReplyId = message.Interactive?.ButtonReply?.Id ?? message.Interactive?.ListReply?.Id;
                interactiveReplyTitle = message.Interactive?.ButtonReply?.Title ?? message.Interactive?.ListReply?.Title;
                content = interactiveReplyTitle ?? interactiveReplyId ?? string.Empty;
            }
        }
        else if (string.Equals(messageType, "location", StringComparison.OrdinalIgnoreCase))
        {
            content = BuildLocationContent(message.Location);
        }
        else if (string.Equals(messageType, "contacts", StringComparison.OrdinalIgnoreCase))
        {
            content = BuildContactsContent(message.Contacts);
        }
        else if (string.Equals(messageType, "reaction", StringComparison.OrdinalIgnoreCase))
        {
            content = BuildReactionContent(message.Reaction);
        }
        else if (string.Equals(messageType, "order", StringComparison.OrdinalIgnoreCase))
        {
            content = BuildOrderContent(message.Order);
        }
        else if (string.Equals(messageType, "system", StringComparison.OrdinalIgnoreCase))
        {
            content = BuildSystemContent(message.System);
        }
        else
        {
            var media = ResolveMedia(message, messageType);
            if (media is not null)
            {
                mediaUrl = media.Id;
                mediaMimeType = media.MimeType;
                fileName = media.FileName;
                content = media.Caption ?? string.Empty;
            }
            else
            {
                content = BuildFallbackContent(message, messageType);
            }
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
                interactiveType,
                interactivePayloadJson,
                occurredAtUtc,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist inbound message {MetaId} for company {CompanyId}.", message.Id, companyId);
        }
    }

    private static WebhookMedia? ResolveMedia(WebhookMessage message, string messageType)
    {
        return messageType switch
        {
            "image" => message.Image,
            "video" => message.Video,
            "audio" => message.Audio,
            "document" => message.Document,
            "sticker" => message.Sticker,
            _ => message.Image ?? message.Video ?? message.Audio ?? message.Document ?? message.Sticker
        };
    }

    private static string BuildLocationContent(WebhookLocation? location)
    {
        if (location is null)
        {
            return string.Empty;
        }

        return JsonSerializer.Serialize(new
        {
            type = "location",
            name = location.Name,
            address = location.Address,
            latitude = location.Latitude,
            longitude = location.Longitude,
            url = location.Url
        });
    }

    private static string BuildContactsContent(IReadOnlyCollection<WebhookInboundContact>? contacts)
    {
        if (contacts is null || contacts.Count == 0)
        {
            return string.Empty;
        }

        var simplifiedContacts = contacts.Select(contact => new
        {
            name = ResolveContactName(contact),
            phones = (contact.Phones ?? [])
                .Select(phone => phone.Phone ?? phone.WaId)
                .Where(phone => !string.IsNullOrWhiteSpace(phone))
                .ToArray(),
            emails = (contact.Emails ?? [])
                .Select(email => email.Email)
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .ToArray()
        }).ToArray();

        return JsonSerializer.Serialize(new
        {
            type = "contacts",
            contacts = simplifiedContacts
        });
    }

    private static string BuildReactionContent(WebhookReaction? reaction)
    {
        if (reaction is null)
        {
            return string.Empty;
        }

        return JsonSerializer.Serialize(new
        {
            type = "reaction",
            emoji = reaction.Emoji,
            messageId = reaction.MessageId
        });
    }

    private static string BuildOrderContent(WebhookOrder? order)
    {
        if (order is null)
        {
            return string.Empty;
        }

        var items = (order.ProductItems ?? [])
            .Select(item => new
            {
                productRetailerId = item.ProductRetailerId,
                quantity = item.Quantity,
                itemPrice = item.ItemPrice,
                currency = item.Currency
            })
            .ToArray();

        return JsonSerializer.Serialize(new
        {
            type = "order",
            catalogId = order.CatalogId,
            text = order.Text,
            productItems = items
        });
    }

    private static string BuildSystemContent(WebhookSystemMessage? systemMessage)
    {
        if (systemMessage is null)
        {
            return string.Empty;
        }

        return JsonSerializer.Serialize(new
        {
            type = "system",
            body = systemMessage.Body,
            systemType = systemMessage.Type,
            newWaId = systemMessage.NewWaId,
            identity = systemMessage.Identity
        });
    }

    private static string BuildFallbackContent(WebhookMessage message, string messageType)
    {
        if (!string.IsNullOrWhiteSpace(message.Text?.Body))
        {
            return message.Text.Body;
        }

        if (message.AdditionalData is null || message.AdditionalData.Count == 0)
        {
            return string.Empty;
        }

        if (message.AdditionalData.TryGetValue(messageType, out var typedNode))
        {
            var typedContent = ConvertJsonNodeToString(typedNode);
            if (!string.IsNullOrWhiteSpace(typedContent))
            {
                return typedContent;
            }
        }

        foreach (var (_, value) in message.AdditionalData)
        {
            var content = ConvertJsonNodeToString(value);
            if (!string.IsNullOrWhiteSpace(content))
            {
                return content;
            }
        }

        return string.Empty;
    }

    private static string ResolveContactName(WebhookInboundContact contact)
    {
        var formatted = contact.Name?.FormattedName;
        if (!string.IsNullOrWhiteSpace(formatted))
        {
            return formatted.Trim();
        }

        var first = contact.Name?.FirstName?.Trim();
        var last = contact.Name?.LastName?.Trim();
        return string.Join(' ', new[] { first, last }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string ConvertJsonNodeToString(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => bool.TrueString.ToLowerInvariant(),
            JsonValueKind.False => bool.FalseString.ToLowerInvariant(),
            JsonValueKind.Object => value.GetRawText(),
            JsonValueKind.Array => value.GetRawText(),
            _ => string.Empty
        };
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
