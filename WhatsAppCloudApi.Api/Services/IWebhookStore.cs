using System.Collections.Generic;

namespace WhatsAppCloudApi.Api.Services;

public interface IWebhookStore
{
    void Add(WebhookLogEntry entry);
    IReadOnlyList<WebhookLogEntry> GetAll();
}

public sealed class WebhookLogEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public DateTimeOffset Timestamp { get; set; }
    public string Payload { get; set; } = string.Empty;
    public string? Summary { get; set; }
}
