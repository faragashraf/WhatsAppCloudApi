namespace WhatsAppCloudApi.Domain.Entities;

public sealed class RoutingTeam
{
    public int RoutingTeamId { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public bool AutoAssignmentEnabled { get; set; } = true;
    public bool ManualAssignmentEnabled { get; set; } = true;
    public DateTime? LastAutoAssignedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public Company? Company { get; set; }
    public ICollection<RoutingTeamMember> Members { get; set; } = [];
    public ICollection<Conversation> Conversations { get; set; } = [];
    public ICollection<LeadDepartment> LeadDepartments { get; set; } = [];
}
