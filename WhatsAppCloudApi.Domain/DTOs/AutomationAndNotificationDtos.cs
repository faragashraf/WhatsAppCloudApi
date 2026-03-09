using System.ComponentModel.DataAnnotations;

namespace WhatsAppCloudApi.Domain.DTOs;

// ─── Automation Rule DTOs ───────────────────────────────────
public sealed class AutomationRuleUpsertRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public string TriggerType { get; set; } = "keyword";

    [Required]
    public string TriggerValue { get; set; } = string.Empty;

    [Required]
    public string ResponseType { get; set; } = "text";

    [Required]
    public string ResponseValue { get; set; } = string.Empty;

    public string? TemplateName { get; set; }
    public string? LanguageCode { get; set; }
    public int Priority { get; set; } = 100;
    public bool IsActive { get; set; } = true;
}

// ─── Notification DTOs ──────────────────────────────────────
public sealed class NotificationQueryParams
{
    public bool? IsRead { get; set; }
    public string? Category { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public sealed class CreateNotificationRequest
{
    [Required]
    public string Type { get; set; } = "info";

    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Body { get; set; } = string.Empty;

    public string? Category { get; set; }
    public string? MetadataJson { get; set; }
    public int? TargetUserId { get; set; }
}
