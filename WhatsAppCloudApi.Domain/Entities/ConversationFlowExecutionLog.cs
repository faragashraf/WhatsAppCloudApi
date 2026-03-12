namespace WhatsAppCloudApi.Domain.Entities;

public sealed class ConversationFlowExecutionLog
{
    public long ConversationFlowExecutionLogId { get; set; }
    public int CompanyId { get; set; }
    public long ConversationFlowId { get; set; }
    public long? ConversationFlowSessionId { get; set; }
    public long? ConversationId { get; set; }
    public long? ContactId { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? Direction { get; set; }
    public string? Message { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Company? Company { get; set; }
    public ConversationFlow? ConversationFlow { get; set; }
    public ConversationFlowSession? ConversationFlowSession { get; set; }
    public Conversation? Conversation { get; set; }
    public Contact? Contact { get; set; }
}
