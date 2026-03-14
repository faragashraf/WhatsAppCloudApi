using System.ComponentModel.DataAnnotations;

namespace WhatsAppCloudApi.Domain.DTOs;

public sealed class UpdateCompanyRoutingSettingsRequest
{
    [Required, MaxLength(20)]
    public string AssignmentMode { get; set; } = "MANUAL";

    [Required, MaxLength(30)]
    public string AutoAssignmentStrategy { get; set; } = "ROUND_ROBIN";

    public bool RespectExistingContactOwner { get; set; } = true;
    public bool ReassignWhenOwnerInactive { get; set; } = true;
    public bool ManualReassignmentUpdatesContactOwner { get; set; }
}

public sealed class CompanyRoutingSettingsDto
{
    public int CompanyId { get; set; }
    public string AssignmentMode { get; set; } = "MANUAL";
    public string AutoAssignmentStrategy { get; set; } = "ROUND_ROBIN";
    public bool RespectExistingContactOwner { get; set; }
    public bool ReassignWhenOwnerInactive { get; set; }
    public bool ManualReassignmentUpdatesContactOwner { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

public sealed class UpdateCompanyUserRoutingSettingsRequest
{
    public bool CanReceiveManualAssignments { get; set; }
    public bool CanReceiveAutoAssignments { get; set; }
}

public sealed class CompanyUserRoutingSettingsDto
{
    public int CompanyUserId { get; set; }
    public int CompanyId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool CanReceiveManualAssignments { get; set; }
    public bool CanReceiveAutoAssignments { get; set; }
    public DateTime? LastAutoAssignedAtUtc { get; set; }
}

public sealed class RoutingTeamDto
{
    public int RoutingTeamId { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public bool AutoAssignmentEnabled { get; set; }
    public bool ManualAssignmentEnabled { get; set; }
    public DateTime? LastAutoAssignedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public int MemberCount { get; set; }
    public List<RoutingTeamMemberDto> Members { get; set; } = [];
}

public sealed class RoutingTeamMemberDto
{
    public int RoutingTeamMemberId { get; set; }
    public int RoutingTeamId { get; set; }
    public int CompanyUserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool CanReceiveManualAssignments { get; set; }
    public bool CanReceiveAutoAssignments { get; set; }
    public DateTime? LastAutoAssignedAtUtc { get; set; }
}

public sealed class CreateRoutingTeamRequest
{
    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
    public bool AutoAssignmentEnabled { get; set; } = true;
    public bool ManualAssignmentEnabled { get; set; } = true;
    public List<int> MemberUserIds { get; set; } = [];
}

public sealed class UpdateRoutingTeamRequest
{
    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
    public bool AutoAssignmentEnabled { get; set; } = true;
    public bool ManualAssignmentEnabled { get; set; } = true;
}

public sealed class UpdateRoutingTeamMembersRequest
{
    public List<int> MemberUserIds { get; set; } = [];
}

public sealed class UpdateContactOwnerRequest
{
    [Range(1, int.MaxValue)]
    public int? UserId { get; set; }

    [MaxLength(100)]
    public string? Reason { get; set; }
}
