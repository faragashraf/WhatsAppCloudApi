using System.ComponentModel.DataAnnotations;

namespace WhatsAppCloudApi.Domain.DTOs;

// ─── Campaign DTOs ──────────────────────────────────────────
public sealed class CampaignCreateRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public string TemplateName { get; set; } = string.Empty;

    [Required]
    public string LanguageCode { get; set; } = "ar";

    public string? TemplateParametersJson { get; set; }
    public int? WhatsAppPhoneNumberId { get; set; }
    public DateTime? ScheduledAtUtc { get; set; }
    public List<string> PhoneNumbers { get; set; } = [];
    public List<long> ContactIds { get; set; } = [];
}

public sealed class CampaignUpdateRequest
{
    [MaxLength(200)]
    public string? Name { get; set; }

    public string? Description { get; set; }
    public DateTime? ScheduledAtUtc { get; set; }
}

public sealed class CampaignQueryParams
{
    public string? Status { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
