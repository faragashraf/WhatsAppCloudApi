using System.Text.Json.Serialization;

namespace WhatsAppCloudApi.Domain.Entities;

public sealed class MetaBusinessAccount
{
    public int MetaBusinessAccountId { get; set; }
    public int CompanyId { get; set; }
    public string BusinessId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    [JsonIgnore]
    public string? AccessToken { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public Company? Company { get; set; }
    public ICollection<WhatsAppAccount> WhatsAppAccounts { get; set; } = [];
}
