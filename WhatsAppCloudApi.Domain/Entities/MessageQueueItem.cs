namespace WhatsAppCloudApi.Domain.Entities;

public sealed class MessageQueueItem
{
    public long MessageQueueId { get; set; }
    public long MessageId { get; set; }
    public int CompanyId { get; set; }
    public string Status { get; set; } = "PENDING";
    public string PayloadJson { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public Company? Company { get; set; }
    public Message? Message { get; set; }
}
