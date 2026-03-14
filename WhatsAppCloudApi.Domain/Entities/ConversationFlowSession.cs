namespace WhatsAppCloudApi.Domain.Entities;

public sealed class ConversationFlowSession
{
    public long ConversationFlowSessionId { get; set; }
    public int CompanyId { get; set; }
    public long ConversationFlowId { get; set; }
    public long ConversationId { get; set; }
    public long ContactId { get; set; }
    public int FlowVersion { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public string CurrentNodeId { get; set; } = "start";
    public string VariablesJson { get; set; } = "{}";
    public int InvalidReplyCount { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastInteractionAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public Company? Company { get; set; }
    public ConversationFlow? ConversationFlow { get; set; }
    public Conversation? Conversation { get; set; }
    public Contact? Contact { get; set; }
    public ICollection<ConversationFlowExecutionLog> ExecutionLogs { get; set; } = [];
    public ICollection<ConversationFlowFormSubmission> FormSubmissions { get; set; } = [];
    public ICollection<LeadRecord> LeadRecords { get; set; } = [];
}
