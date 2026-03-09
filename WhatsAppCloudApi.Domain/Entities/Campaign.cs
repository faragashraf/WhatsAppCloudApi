namespace WhatsAppCloudApi.Domain.Entities;

public sealed class Campaign
{
    public long CampaignId { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = "ar";
    public string? TemplateParametersJson { get; set; }
    public int? WhatsAppPhoneNumberId { get; set; }
    public string Status { get; set; } = "DRAFT"; // DRAFT | SCHEDULED | RUNNING | COMPLETED | CANCELLED
    public DateTime? ScheduledAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int TotalContacts { get; set; }
    public int SentCount { get; set; }
    public int DeliveredCount { get; set; }
    public int ReadCount { get; set; }
    public int FailedCount { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public Company? Company { get; set; }
    public WhatsAppPhoneNumber? WhatsAppPhoneNumber { get; set; }
    public ICollection<CampaignContact> CampaignContacts { get; set; } = [];
}
