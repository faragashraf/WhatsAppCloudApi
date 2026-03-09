using System.Text.Json;
using System.Text.Json.Serialization;
using WhatsAppCloudApi.Domain.Models;

namespace WhatsAppCloudApi.Domain.Entities;

public sealed class CompanyUser
{
    public int CompanyUserId { get; set; }
    public int CompanyId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    [JsonIgnore]
    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = "Admin";
    public bool IsActive { get; set; } = true;

    /// <summary>JSON-serialized UserPermissions. NULL = default permissions based on role.</summary>
    public string? PermissionsJson { get; set; }

    [JsonIgnore]
    public string? RefreshToken { get; set; }

    [JsonIgnore]
    public DateTime? RefreshTokenExpiryUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public Company? Company { get; set; }

    /// <summary>Returns the effective permissions for this user (admin = full access).</summary>
    [JsonIgnore]
    public UserPermissions EffectivePermissions
    {
        get
        {
            if (string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase))
                return UserPermissions.FullAccess();

            if (!string.IsNullOrEmpty(PermissionsJson))
            {
                try
                {
                    return JsonSerializer.Deserialize<UserPermissions>(PermissionsJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                           ?? UserPermissions.MemberDefault();
                }
                catch { /* fall through */ }
            }

            return UserPermissions.MemberDefault();
        }
    }
}
