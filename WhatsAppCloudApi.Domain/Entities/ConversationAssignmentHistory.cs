namespace WhatsAppCloudApi.Domain.Entities;

public sealed class ConversationAssignmentHistory
{
    public long ConversationAssignmentHistoryId { get; set; }
    public int CompanyId { get; set; }
    public long ConversationId { get; set; }
    public long? ContactId { get; set; }
    public int? PreviousAssignedTeamId { get; set; }
    public int? NewAssignedTeamId { get; set; }
    public int? PreviousAssignedUserId { get; set; }
    public int? NewAssignedUserId { get; set; }
    public int? PreviousOwnerUserId { get; set; }
    public int? NewOwnerUserId { get; set; }
    public string AssignmentMode { get; set; } = "MANUAL";
    public string Reason { get; set; } = string.Empty;
    public int? ChangedByUserId { get; set; }
    public string? Notes { get; set; }
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;

    public Company? Company { get; set; }
    public Conversation? Conversation { get; set; }
    public Contact? Contact { get; set; }
    public RoutingTeam? PreviousAssignedTeam { get; set; }
    public RoutingTeam? NewAssignedTeam { get; set; }
    public CompanyUser? PreviousAssignedUser { get; set; }
    public CompanyUser? NewAssignedUser { get; set; }
    public CompanyUser? PreviousOwnerUser { get; set; }
    public CompanyUser? NewOwnerUser { get; set; }
    public CompanyUser? ChangedByUser { get; set; }
}
