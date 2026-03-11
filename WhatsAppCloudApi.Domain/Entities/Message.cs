namespace WhatsAppCloudApi.Domain.Entities;

public sealed class Message
{
    public long MessageId { get; set; }
    public int CompanyId { get; set; }
    public int? WhatsAppPhoneNumberId { get; set; }
    public long? ContactId { get; set; }
    public long? ConversationId { get; set; }
    public int? CreatedByUserId { get; set; }
    public string ToNumber { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string MessageBody { get; set; } = string.Empty;
    public string Status { get; set; } = "PENDING";
    public string Source { get; set; } = "DIRECT";
    public string? ExternalMessageId { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public Company? Company { get; set; }
    public WhatsAppPhoneNumber? WhatsAppPhoneNumber { get; set; }
    public Contact? Contact { get; set; }
    public Conversation? Conversation { get; set; }
    public CompanyUser? CreatedByUser { get; set; }
    public ICollection<MessageQueueItem> QueueItems { get; set; } = [];
    public ICollection<ConversationMessage> ConversationMessages { get; set; } = [];
}
