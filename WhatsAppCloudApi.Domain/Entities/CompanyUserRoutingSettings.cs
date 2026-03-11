namespace WhatsAppCloudApi.Domain.Entities;

public sealed class CompanyUserRoutingSettings
{
    public int CompanyUserRoutingSettingsId { get; set; }
    public int CompanyId { get; set; }
    public int CompanyUserId { get; set; }
    public bool CanReceiveManualAssignments { get; set; } = true;
    public bool CanReceiveAutoAssignments { get; set; } = true;
    public DateTime? LastAutoAssignedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public Company? Company { get; set; }
    public CompanyUser? CompanyUser { get; set; }
}
