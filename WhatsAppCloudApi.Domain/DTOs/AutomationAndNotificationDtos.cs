using System.ComponentModel.DataAnnotations;

namespace WhatsAppCloudApi.Domain.DTOs;

// ─── Automation Rule DTOs ───────────────────────────────────
public sealed class AutomationRuleUpsertRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required, MaxLength(50)]
    public string TriggerType { get; set; } = "keyword";

    [MaxLength(500)]
    public string? TriggerValue { get; set; }

    [Required, MaxLength(50)]
    public string ResponseType { get; set; } = "text";

    [MaxLength(4000)]
    public string? ResponseValue { get; set; }

    [MaxLength(200)]
    public string? TemplateName { get; set; }

    [MaxLength(20)]
    public string? LanguageCode { get; set; }

    [Range(0, 10000)]
    public int Priority { get; set; } = 100;
    public bool IsActive { get; set; } = true;
}

// ─── Notification DTOs ──────────────────────────────────────
public sealed class NotificationQueryParams
{
    public bool? IsRead { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    [Range(1, 1000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 25;
}

public sealed class CreateNotificationRequest
{
    [Required, MaxLength(50)]
    public string Type { get; set; } = "info";

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(4000)]
    public string Body { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Category { get; set; }

    [MaxLength(4000)]
    public string? MetadataJson { get; set; }
    public int? TargetUserId { get; set; }
}
