namespace WhatsAppCloudApi.Domain.DTOs;

public sealed class ContactOwnerSummaryDto
{
    public int CompanyUserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class ContactConversationSummaryDto
{
    public long ConversationId { get; set; }
    public int? WhatsAppPhoneNumberId { get; set; }
    public string ContactNumber { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Status { get; set; }
    public int UnreadCount { get; set; }
    public int? AssignedUserId { get; set; }
    public string? AssignedUserName { get; set; }
    public DateTime? LastMessageAtUtc { get; set; }
    public DateTime? LastInboundMessageAtUtc { get; set; }
    public string? LastMessageContent { get; set; }
    public string? LastMessageType { get; set; }
}

public sealed class ConversationAssignmentHistoryDto
{
    public long ConversationAssignmentHistoryId { get; set; }
    public long ConversationId { get; set; }
    public long? ContactId { get; set; }
    public int? PreviousAssignedUserId { get; set; }
    public string? PreviousAssignedUserName { get; set; }
    public int? NewAssignedUserId { get; set; }
    public string? NewAssignedUserName { get; set; }
    public int? PreviousOwnerUserId { get; set; }
    public string? PreviousOwnerUserName { get; set; }
    public int? NewOwnerUserId { get; set; }
    public string? NewOwnerUserName { get; set; }
    public int? ChangedByUserId { get; set; }
    public string? ChangedByUserName { get; set; }
    public string AssignmentMode { get; set; } = "MANUAL";
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime ChangedAtUtc { get; set; }
}

public sealed class ContactProfileDto
{
    public long ContactId { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Tags { get; set; }
    public string? Source { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public DateTime FirstSeenAtUtc { get; set; }
    public DateTime? LastSeenAtUtc { get; set; }
    public DateTime? LastInboundMessageAtUtc { get; set; }
    public DateTime? LastOutboundMessageAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public ContactOwnerSummaryDto? Owner { get; set; }
    public int ConversationCount { get; set; }
    public int MessageCount { get; set; }
    public List<ContactConversationSummaryDto> RecentConversations { get; set; } = [];
    public List<ConversationAssignmentHistoryDto> AssignmentHistory { get; set; } = [];
}
