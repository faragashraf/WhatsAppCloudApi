using System.Text.Json.Serialization;

namespace WhatsAppCloudApi.Domain.Models;

public sealed class WebhookPayload
{
    [JsonPropertyName("object")]
    public string? Object { get; set; }

    [JsonPropertyName("entry")]
    public List<WebhookEntry> Entry { get; set; } = [];
}

public sealed class WebhookEntry
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("changes")]
    public List<WebhookChange> Changes { get; set; } = [];
}

public sealed class WebhookChange
{
    [JsonPropertyName("field")]
    public string? Field { get; set; }

    [JsonPropertyName("value")]
    public WebhookValue? Value { get; set; }
}

public sealed class WebhookValue
{
    [JsonPropertyName("messaging_product")]
    public string? MessagingProduct { get; set; }
    [JsonPropertyName("messages")]
    public List<WebhookMessage>? Messages { get; set; }

    [JsonPropertyName("statuses")]
    public List<WebhookStatus>? Statuses { get; set; }

    [JsonPropertyName("contacts")]
    public List<WebhookContact>? Contacts { get; set; }

    [JsonPropertyName("metadata")]
    public WebhookMetadata? Metadata { get; set; }
}

public sealed class WebhookContact
{
    [JsonPropertyName("profile")]
    public WebhookContactProfile? Profile { get; set; }

    [JsonPropertyName("wa_id")]
    public string? WaId { get; set; }
}

public sealed class WebhookContactProfile
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public sealed class WebhookMetadata
{
    [JsonPropertyName("display_phone_number")]
    public string? DisplayPhoneNumber { get; set; }

    [JsonPropertyName("phone_number_id")]
    public string? PhoneNumberId { get; set; }
}

public sealed class WebhookMessage
{
    [JsonPropertyName("from")]
    public string? From { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("text")]
    public WebhookText? Text { get; set; }

    [JsonPropertyName("image")]
    public WebhookMedia? Image { get; set; }

    [JsonPropertyName("video")]
    public WebhookMedia? Video { get; set; }

    [JsonPropertyName("audio")]
    public WebhookMedia? Audio { get; set; }

    [JsonPropertyName("document")]
    public WebhookMedia? Document { get; set; }

    [JsonPropertyName("sticker")]
    public WebhookMedia? Sticker { get; set; }
}

public sealed class WebhookText
{
    [JsonPropertyName("body")]
    public string? Body { get; set; }
}

public sealed class WebhookMedia
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("mime_type")]
    public string? MimeType { get; set; }

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }

    [JsonPropertyName("caption")]
    public string? Caption { get; set; }

    [JsonPropertyName("filename")]
    public string? FileName { get; set; }
}

public sealed class WebhookStatus
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("recipient_id")]
    public string? RecipientId { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }
}
