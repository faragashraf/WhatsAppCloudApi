namespace WhatsAppCloudApi.Domain.Entities;

public sealed class EmailNotificationRule
{
    public long EmailNotificationRuleId { get; set; }
    public int? CompanyId { get; set; }
    public int EmailAccountId { get; set; }
    public int? CreatedByUserId { get; set; }
    public string Scope { get; set; } = "COMPANY";
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "general";
    public string TriggerType { get; set; } = "COMPANY_SUBSCRIPTION_EXPIRY";
    public int LeadTimeDays { get; set; } = 7;
    public string RecipientMode { get; set; } = "COMPANY_ADMINS";
    public string? RecipientsJson { get; set; }
    public string SubjectTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
    public bool IsBodyHtml { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastTriggeredAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public Company? Company { get; set; }
    public EmailAccount? EmailAccount { get; set; }
    public CompanyUser? CreatedByUser { get; set; }
    public ICollection<EmailQueueItem> QueueItems { get; set; } = [];
}
