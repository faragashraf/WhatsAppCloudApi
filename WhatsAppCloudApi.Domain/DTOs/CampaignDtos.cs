using System.ComponentModel.DataAnnotations;

namespace WhatsAppCloudApi.Domain.DTOs;

// ─── Campaign DTOs ──────────────────────────────────────────
public sealed class CampaignCreateRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required, MaxLength(200)]
    public string TemplateName { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string LanguageCode { get; set; } = "ar";

    [MaxLength(8000)]
    public string? TemplateParametersJson { get; set; }

    public int? WhatsAppPhoneNumberId { get; set; }
    public DateTime? ScheduledAtUtc { get; set; }

    [MaxLength(5000)]
    public List<string> PhoneNumbers { get; set; } = [];

    [MaxLength(5000)]
    public List<long> ContactIds { get; set; } = [];
}

public sealed class CampaignUpdateRequest
{
    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }
    public DateTime? ScheduledAtUtc { get; set; }
}

public sealed class CampaignQueryParams
{
    [MaxLength(50)]
    public string? Status { get; set; }

    [MaxLength(200)]
    public string? Search { get; set; }

    [Range(1, 1000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 25;
}

public sealed class CampaignRecipientValidationRequest
{
    public int? WhatsAppPhoneNumberId { get; set; }

    [MaxLength(5000)]
    public List<string> PhoneNumbers { get; set; } = [];

    [MaxLength(5000)]
    public List<long> ContactIds { get; set; } = [];
}

public sealed class CampaignRecipientValidationItemDto
{
    public string PhoneNumber { get; set; } = string.Empty;
    public long? ContactId { get; set; }
    public bool IsWhatsAppAccountConfirmed { get; set; }
    public string Status { get; set; } = "UNVERIFIED"; // CONFIRMED | NOT_WHATSAPP | INVALID | ERROR | UNVERIFIED
    public string? WaId { get; set; }
    public string? Error { get; set; }
}

public sealed class CampaignRecipientValidationResultDto
{
    public int TotalRecipients { get; set; }
    public int ConfirmedRecipients { get; set; }
    public int NotWhatsAppRecipients { get; set; }
    public int InvalidRecipients { get; set; }
    public int ErrorRecipients { get; set; }
    public List<CampaignRecipientValidationItemDto> Recipients { get; set; } = [];
}
