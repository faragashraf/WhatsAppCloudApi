namespace WhatsAppCloudApi.Domain.Entities;

public sealed class LeadDepartment
{
    public int LeadDepartmentId { get; set; }
    public int CompanyId { get; set; }
    public int? RoutingTeamId { get; set; }
    public string DepartmentKey { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public Company? Company { get; set; }
    public RoutingTeam? RoutingTeam { get; set; }
    public ICollection<LeadRecord> Leads { get; set; } = [];
}
