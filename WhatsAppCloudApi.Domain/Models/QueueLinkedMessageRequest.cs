using WhatsAppCloudApi.Domain.Entities;

namespace WhatsAppCloudApi.Domain.Models;

public sealed class QueueLinkedMessageRequest
{
    public int CompanyId { get; set; }
    public int? WhatsAppPhoneNumberId { get; set; }
    public long? ContactId { get; set; }
    public long? ConversationId { get; set; }
    public int? CreatedByUserId { get; set; }
    public string ToNumber { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string MessageBody { get; set; } = string.Empty;
    public string Source { get; set; } = "DIRECT";
    public string ConversationMessageType { get; set; } = "text";
    public string ConversationContent { get; set; } = string.Empty;
    public string? MediaUrl { get; set; }
    public string? MediaMimeType { get; set; }
    public string? FileName { get; set; }
    public string ConversationMessageStatus { get; set; } = "sending";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public MessageQueuePayload Payload { get; set; } = new();
}

public sealed class QueuedLinkedMessageResult
{
    public Message Message { get; init; } = null!;
    public ConversationMessage ConversationMessage { get; init; } = null!;
}
