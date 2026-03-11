namespace WhatsAppCloudApi.Domain.Entities;

public sealed class EmailQueueItem
{
    public long EmailQueueItemId { get; set; }
    public int? CompanyId { get; set; }
    public int EmailAccountId { get; set; }
    public long? EmailNotificationRuleId { get; set; }
    public int? CreatedByUserId { get; set; }
    public string Scope { get; set; } = "COMPANY";
    public string Category { get; set; } = "manual";
    public string TriggerType { get; set; } = "MANUAL";
    public string ToJson { get; set; } = "[]";
    public string? CcJson { get; set; }
    public string? BccJson { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsBodyHtml { get; set; }
    public string Status { get; set; } = "PENDING";
    public int Priority { get; set; } = 100;
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public string? DeduplicationKey { get; set; }
    public DateTime ScheduledAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public Company? Company { get; set; }
    public EmailAccount? EmailAccount { get; set; }
    public EmailNotificationRule? EmailNotificationRule { get; set; }
    public CompanyUser? CreatedByUser { get; set; }
    public ICollection<EmailQueueAttachment> Attachments { get; set; } = [];
}
