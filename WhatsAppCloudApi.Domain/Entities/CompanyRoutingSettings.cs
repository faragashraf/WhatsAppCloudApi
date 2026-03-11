namespace WhatsAppCloudApi.Domain.Entities;

public sealed class CompanyRoutingSettings
{
    public int CompanyRoutingSettingsId { get; set; }
    public int CompanyId { get; set; }
    public string AssignmentMode { get; set; } = "MANUAL";
    public string AutoAssignmentStrategy { get; set; } = "ROUND_ROBIN";
    public bool RespectExistingContactOwner { get; set; } = true;
    public bool ReassignWhenOwnerInactive { get; set; } = true;
    public bool ManualReassignmentUpdatesContactOwner { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public Company? Company { get; set; }
}
