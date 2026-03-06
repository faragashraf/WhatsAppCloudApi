using System.ComponentModel.DataAnnotations.Schema;

namespace WhatsAppCloudApi.Domain.Entities;

public sealed class CompanyUser
{
    public int CompanyUserId { get; set; }
    public int CompanyId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Admin";
    public bool IsActive { get; set; } = true;

    [NotMapped]
    public string? RefreshToken { get; set; }

    [NotMapped]
    public DateTime? RefreshTokenExpiryUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public DateTime? UpdatedAtUtc { get; set; }

    public Company? Company { get; set; }
}
