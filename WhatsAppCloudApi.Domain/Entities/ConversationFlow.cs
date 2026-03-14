namespace WhatsAppCloudApi.Domain.Entities;

public sealed class ConversationFlow
{
    public long ConversationFlowId { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EntryTriggerType { get; set; } = "any_message";
    public string? EntryTriggerValue { get; set; }
    public string DraftDefinitionJson { get; set; } = "{\"nodes\":[],\"edges\":[]}";
    public string? PublishedDefinitionJson { get; set; }
    public int DraftVersion { get; set; } = 1;
    public int? PublishedVersion { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPublished { get; set; }
    public long TriggerCount { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }

    public Company? Company { get; set; }
    public ICollection<ConversationFlowSession> Sessions { get; set; } = [];
    public ICollection<ConversationFlowExecutionLog> ExecutionLogs { get; set; } = [];
    public ICollection<ConversationFlowFormSubmission> FormSubmissions { get; set; } = [];
    public ICollection<LeadRecord> LeadRecords { get; set; } = [];
}
