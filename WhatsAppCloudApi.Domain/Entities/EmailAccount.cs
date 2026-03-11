namespace WhatsAppCloudApi.Domain.Entities;

public sealed class EmailAccount
{
    public int EmailAccountId { get; set; }
    public int? CompanyId { get; set; }
    public int? CreatedByUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string? ReplyToAddress { get; set; }
    public string FromName { get; set; } = string.Empty;
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string PasswordProtected { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public Company? Company { get; set; }
    public CompanyUser? CreatedByUser { get; set; }
    public ICollection<EmailQueueItem> QueueItems { get; set; } = [];
    public ICollection<EmailNotificationRule> NotificationRules { get; set; } = [];
}
