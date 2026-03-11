using System.ComponentModel.DataAnnotations;
using WhatsAppCloudApi.Domain.Models;

namespace WhatsAppCloudApi.Domain.DTOs;

public sealed class CompanyUpsertRequest
{
    [MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Name { get; set; }

    [EmailAddress, MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(50)]
    public string? Status { get; set; }

    public bool? IsActive { get; set; }
}

public sealed class CompanyUserUpsertRequest
{
    [Required, MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [MinLength(10), MaxLength(128)]
    public string? Password { get; set; }

    [Required, MaxLength(50)]
    public string Role { get; set; } = "Member";

    public bool IsActive { get; set; } = true;
    public UserPermissions? Permissions { get; set; }
}
