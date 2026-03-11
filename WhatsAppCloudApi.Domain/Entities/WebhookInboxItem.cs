namespace WhatsAppCloudApi.Domain.Entities;

public sealed class WebhookInboxItem
{
    public long WebhookInboxId { get; set; }
    public int CompanyId { get; set; }
    public int WhatsAppPhoneNumberId { get; set; }
    public string PhoneNumberId { get; set; } = string.Empty;
    public string Status { get; set; } = "PENDING";
    public string PayloadJson { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public Company? Company { get; set; }
}
