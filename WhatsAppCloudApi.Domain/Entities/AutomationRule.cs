namespace WhatsAppCloudApi.Domain.Entities;

public sealed class AutomationRule
{
    public long AutomationRuleId { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TriggerType { get; set; } = "keyword"; // keyword | contains | regex | exact
    public string TriggerValue { get; set; } = string.Empty;
    public string ResponseType { get; set; } = "text"; // text | template
    public string ResponseValue { get; set; } = string.Empty;
    public string? TemplateName { get; set; }
    public string? LanguageCode { get; set; }
    public int Priority { get; set; } = 100;
    public bool IsActive { get; set; } = true;
    public long TriggerCount { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public Company? Company { get; set; }
}
