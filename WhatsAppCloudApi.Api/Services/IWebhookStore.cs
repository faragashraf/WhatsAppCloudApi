using System.Collections.Generic;

namespace WhatsAppCloudApi.Api.Services;

public interface IWebhookStore
{
    Task AddAsync(WebhookLogEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WebhookLogEntry>> GetAllAsync(int companyId, int take = 500, CancellationToken cancellationToken = default);
}

public sealed class WebhookLogEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public int CompanyId { get; set; }
    public string PhoneNumberId { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string Payload { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public bool? SignatureValid { get; set; }
    public string? CorrelationId { get; set; }
}
