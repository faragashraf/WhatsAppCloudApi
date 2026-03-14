namespace WhatsAppCloudApi.Domain.Entities;

public sealed class LeadRecord
{
    public long LeadRecordId { get; set; }
    public int CompanyId { get; set; }
    public long ConversationFlowFormSubmissionId { get; set; }
    public long ConversationFlowId { get; set; }
    public long? ConversationFlowSessionId { get; set; }
    public long? ConversationId { get; set; }
    public long? ContactId { get; set; }
    public int? LeadDepartmentId { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = "NEW";
    public string? ExtractedValuesJson { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public Company? Company { get; set; }
    public ConversationFlowFormSubmission? ConversationFlowFormSubmission { get; set; }
    public ConversationFlow? ConversationFlow { get; set; }
    public ConversationFlowSession? ConversationFlowSession { get; set; }
    public Conversation? Conversation { get; set; }
    public Contact? Contact { get; set; }
    public LeadDepartment? LeadDepartment { get; set; }
}

