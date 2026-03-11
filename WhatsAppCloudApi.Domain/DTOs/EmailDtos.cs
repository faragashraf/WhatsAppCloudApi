using System.ComponentModel.DataAnnotations;

namespace WhatsAppCloudApi.Domain.DTOs;

public sealed class EmailQueueDashboardDto
{
    public int ActiveAccountCount { get; set; }
    public int ActiveRuleCount { get; set; }
    public int PendingCount { get; set; }
    public int ScheduledCount { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public int DueInNext24Hours { get; set; }
    public int TotalQueued { get; set; }
}

public sealed class EmailQueueQueryParams
{
    [Range(1, 1000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 25;

    [MaxLength(50)]
    public string? Status { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    [MaxLength(200)]
    public string? Search { get; set; }
}

public sealed class EmailAttachmentRequest
{
    [Required, MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string ContentType { get; set; } = "application/octet-stream";

    [Required]
    public string ContentBase64 { get; set; } = string.Empty;
}

public sealed class QueueEmailRequest
{
    public int EmailAccountId { get; set; }

    [MinLength(1)]
    public List<string> To { get; set; } = [];

    public List<string> Cc { get; set; } = [];
    public List<string> Bcc { get; set; } = [];

    [Required, MaxLength(300)]
    public string Subject { get; set; } = string.Empty;

    [Required, MaxLength(20000)]
    public string Body { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Category { get; set; } = "manual";

    [MaxLength(100)]
    public string TriggerType { get; set; } = "MANUAL";

    public bool IsBodyHtml { get; set; }

    [Range(0, 1000)]
    public int Priority { get; set; } = 100;

    public DateTime? ScheduledAtUtc { get; set; }

    public List<EmailAttachmentRequest> Attachments { get; set; } = [];
}

public sealed class EmailAttachmentDto
{
    public long EmailQueueAttachmentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}

public sealed class EmailQueueItemDto
{
    public long EmailQueueItemId { get; set; }
    public int? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public int EmailAccountId { get; set; }
    public string EmailAccountName { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string TriggerType { get; set; } = string.Empty;
    public List<string> To { get; set; } = [];
    public List<string> Cc { get; set; } = [];
    public List<string> Bcc { get; set; } = [];
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsBodyHtml { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Priority { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public DateTime ScheduledAtUtc { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public List<EmailAttachmentDto> Attachments { get; set; } = [];
}

public sealed class EmailAccountUpsertRequest
{
    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(200)]
    public string FromAddress { get; set; } = string.Empty;

    [EmailAddress, MaxLength(200)]
    public string? ReplyToAddress { get; set; }

    [Required, MaxLength(150)]
    public string FromName { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string SmtpHost { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int SmtpPort { get; set; } = 587;

    public bool EnableSsl { get; set; } = true;

    [Required, MaxLength(200)]
    public string Username { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Password { get; set; }

    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class EmailAccountDto
{
    public int EmailAccountId { get; set; }
    public int? CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string? ReplyToAddress { get; set; }
    public string FromName { get; set; } = string.Empty;
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; }
    public bool EnableSsl { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool HasPassword { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

public sealed class EmailNotificationRuleUpsertRequest
{
    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    public int EmailAccountId { get; set; }

    [Required, MaxLength(100)]
    public string Category { get; set; } = "subscription";

    [Required, MaxLength(100)]
    public string TriggerType { get; set; } = "COMPANY_SUBSCRIPTION_EXPIRY";

    [Range(0, 365)]
    public int LeadTimeDays { get; set; } = 7;

    [Required, MaxLength(50)]
    public string RecipientMode { get; set; } = "COMPANY_ADMINS";

    public List<string> Recipients { get; set; } = [];

    [Required, MaxLength(300)]
    public string SubjectTemplate { get; set; } = string.Empty;

    [Required, MaxLength(20000)]
    public string BodyTemplate { get; set; } = string.Empty;

    public bool IsBodyHtml { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class EmailNotificationRuleDto
{
    public long EmailNotificationRuleId { get; set; }
    public int? CompanyId { get; set; }
    public int EmailAccountId { get; set; }
    public string EmailAccountName { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string TriggerType { get; set; } = string.Empty;
    public int LeadTimeDays { get; set; }
    public string RecipientMode { get; set; } = string.Empty;
    public List<string> Recipients { get; set; } = [];
    public string SubjectTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
    public bool IsBodyHtml { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastTriggeredAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
