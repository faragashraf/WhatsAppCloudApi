using System.ComponentModel.DataAnnotations;

namespace WhatsAppCloudApi.Domain.DTOs;

public sealed class LeadQueryParams
{
    [Range(1, int.MaxValue)]
    public long? ContactId { get; set; }

    [Range(1, long.MaxValue)]
    public long? ConversationFlowId { get; set; }

    [Range(1, int.MaxValue)]
    public int? LeadDepartmentId { get; set; }

    [MaxLength(100)]
    public string? DepartmentKey { get; set; }

    [MaxLength(40)]
    public string? Source { get; set; }

    [MaxLength(30)]
    public string? Status { get; set; }

    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }

    [Range(1, 1000)]
    public int Page { get; set; } = 1;

    [Range(1, 200)]
    public int PageSize { get; set; } = 50;
}

public sealed class LeadRecordDto
{
    public long LeadRecordId { get; set; }
    public int CompanyId { get; set; }
    public long ConversationFlowFormSubmissionId { get; set; }
    public long ConversationFlowId { get; set; }
    public long? ConversationFlowSessionId { get; set; }
    public long? ConversationId { get; set; }
    public long? ContactId { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhoneNumber { get; set; }
    public int? LeadDepartmentId { get; set; }
    public string? DepartmentKey { get; set; }
    public string? DepartmentNameAr { get; set; }
    public string? DepartmentNameEn { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = "NEW";
    public Dictionary<string, string> ExtractedValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

public sealed class LeadDepartmentDto
{
    public int LeadDepartmentId { get; set; }
    public int CompanyId { get; set; }
    public string DepartmentKey { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public int? RoutingTeamId { get; set; }
    public string? RoutingTeamName { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public int LeadCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

public sealed class LeadSourceSummaryDto
{
    public string Source { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class LeadDepartmentSummaryDto
{
    public int? LeadDepartmentId { get; set; }
    public string DepartmentKey { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class LeadDashboardSummaryDto
{
    public int TotalLeads { get; set; }
    public int NewLeads { get; set; }
    public int ContactLinkedLeads { get; set; }
    public int MetaFlowLeads { get; set; }
    public DateTime? LastLeadAtUtc { get; set; }
    public List<LeadDepartmentSummaryDto> ByDepartment { get; set; } = [];
    public List<LeadSourceSummaryDto> BySource { get; set; } = [];
}

public sealed class UpsertLeadDepartmentRequest
{
    [Required, MaxLength(100)]
    public string DepartmentKey { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string NameAr { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string NameEn { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int? RoutingTeamId { get; set; }

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
