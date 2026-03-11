namespace WhatsAppCloudApi.Domain.Entities;

public sealed class WebhookLog
{
    public long WebhookLogId { get; set; }
    public int CompanyId { get; set; }
    public string PhoneNumberId { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public bool? SignatureValid { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Company? Company { get; set; }
}
