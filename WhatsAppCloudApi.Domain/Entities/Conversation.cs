namespace WhatsAppCloudApi.Domain.Entities;

public sealed class Conversation
{
    public long ConversationId { get; set; }
    public int CompanyId { get; set; }
    public int? WhatsAppPhoneNumberId { get; set; }
    public long ContactId { get; set; }
    public string ContactNumber { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? LastMessageContent { get; set; }
    public string? LastMessageType { get; set; }
    public DateTime? LastMessageAtUtc { get; set; }
    public DateTime? LastInboundMessageAtUtc { get; set; }
    public string Status { get; set; } = "OPEN";
    public int UnreadCount { get; set; }
    public int? AssignedTeamId { get; set; }
    public int? AssignedUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public Company? Company { get; set; }
    public WhatsAppPhoneNumber? WhatsAppPhoneNumber { get; set; }
    public Contact? Contact { get; set; }
    public RoutingTeam? AssignedTeam { get; set; }
    public CompanyUser? AssignedUser { get; set; }
    public ICollection<ConversationMessage> Messages { get; set; } = [];
    public ICollection<Message> OutboundMessages { get; set; } = [];
    public ICollection<ConversationAssignmentHistory> AssignmentHistory { get; set; } = [];
}
