namespace WhatsAppCloudApi.Domain.Entities;

public sealed class Notification
{
    public long NotificationId { get; set; }
    public int CompanyId { get; set; }
    public int? CompanyUserId { get; set; }
    public string Type { get; set; } = "info"; // info | warning | error | success
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Category { get; set; } // token_expiry | webhook_failure | quality_drop | campaign | system
    public string? MetadataJson { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAtUtc { get; set; }

    public Company? Company { get; set; }
    public CompanyUser? CompanyUser { get; set; }
}
