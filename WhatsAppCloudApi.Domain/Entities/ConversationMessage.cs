namespace WhatsAppCloudApi.Domain.Entities;

public sealed class ConversationMessage
{
    public long ConversationMessageId { get; set; }
    public long ConversationId { get; set; }
    public int CompanyId { get; set; }
    public string Direction { get; set; } = "outbound"; // inbound | outbound
    public string? MetaMessageId { get; set; }
    public string MessageType { get; set; } = "text";
    public string Content { get; set; } = string.Empty;
    public string? MediaUrl { get; set; }
    public string? MediaMimeType { get; set; }
    public string Status { get; set; } = "sent"; // sent | delivered | read | failed
    public string? FailureReason { get; set; }
    public bool IsFromAutomation { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public Conversation? Conversation { get; set; }
    public Company? Company { get; set; }
}
