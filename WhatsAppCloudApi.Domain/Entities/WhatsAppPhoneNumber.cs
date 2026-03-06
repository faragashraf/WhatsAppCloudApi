namespace WhatsAppCloudApi.Domain.Entities;

public sealed class WhatsAppPhoneNumber
{
    public int WhatsAppPhoneNumberId { get; set; }
    public int CompanyId { get; set; }
    public string WhatsAppAccountId { get; set; } = string.Empty;
    public string PhoneNumberId { get; set; } = string.Empty;
    public string DisplayPhoneNumber { get; set; } = string.Empty;
    public string? VerifiedName { get; set; }
    public bool IsDefault { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public Company? Company { get; set; }
    public WhatsAppAccount? WhatsAppAccount { get; set; }
    public ICollection<Message> Messages { get; set; } = [];
}
