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
    public List<object>? Messages { get; set; }

    [JsonPropertyName("statuses")]
    public List<object>? Statuses { get; set; }

    [JsonPropertyName("metadata")]
    public object? Metadata { get; set; }
}
