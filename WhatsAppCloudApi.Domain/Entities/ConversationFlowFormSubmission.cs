namespace WhatsAppCloudApi.Domain.Entities;

public sealed class ConversationFlowFormSubmission
{
    public long ConversationFlowFormSubmissionId { get; set; }
    public int CompanyId { get; set; }
    public long ConversationFlowId { get; set; }
    public long? ConversationFlowSessionId { get; set; }
    public long? ConversationId { get; set; }
    public long? ContactId { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? InboundMessageType { get; set; }
    public string? MetaMessageId { get; set; }
    public string? PayloadJson { get; set; }
    public string? ExtractedValuesJson { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Company? Company { get; set; }
    public ConversationFlow? ConversationFlow { get; set; }
    public ConversationFlowSession? ConversationFlowSession { get; set; }
    public Conversation? Conversation { get; set; }
    public Contact? Contact { get; set; }
}
