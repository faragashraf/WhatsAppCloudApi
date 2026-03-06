namespace WhatsAppCloudApi.Domain.Entities;

public sealed class WhatsAppAccount
{
    public string WhatsAppAccountId { get; set; } = string.Empty;
    public int CompanyId { get; set; }
    public int? MetaBusinessAccountId { get; set; }
    public string BusinessAccountId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string VerifyToken { get; set; } = string.Empty;
    public string? AppSecret { get; set; }
    public bool IsDefault { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public Company? Company { get; set; }
    public MetaBusinessAccount? MetaBusinessAccount { get; set; }
    public ICollection<WhatsAppPhoneNumber> PhoneNumbers { get; set; } = [];
}
