namespace WhatsAppCloudApi.Domain.Entities;

public sealed class RoutingTeamMember
{
    public int RoutingTeamMemberId { get; set; }
    public int CompanyId { get; set; }
    public int RoutingTeamId { get; set; }
    public int CompanyUserId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastAutoAssignedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public Company? Company { get; set; }
    public RoutingTeam? RoutingTeam { get; set; }
    public CompanyUser? CompanyUser { get; set; }
}

