using WhatsAppCloudApi.Domain.Models;

namespace WhatsAppCloudApi.Domain.DTOs;

public sealed class CompanyUpsertRequest
{
    public string CompanyName { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Status { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class CompanyUserUpsertRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string Role { get; set; } = "Member";
    public bool IsActive { get; set; } = true;
    public UserPermissions? Permissions { get; set; }
}
