namespace WhatsAppCloudApi.Domain.Entities;

public sealed class WhatsAppPhoneNumber
{
    public int WhatsAppPhoneNumberId { get; set; }
    public int CompanyId { get; set; }
    public string WhatsAppAccountId { get; set; } = string.Empty;
    public string PhoneNumberId { get; set; } = string.Empty;
    public string DisplayPhoneNumber { get; set; } = string.Empty;
    public string? VerifiedName { get; set; }
    public string? CodeVerificationStatus { get; set; }
    public string? QualityRating { get; set; }
    public string? PlatformType { get; set; }
    public string? ThroughputLevel { get; set; }
    public string? LastOnboardedTime { get; set; }
    public bool IsDefault { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime? LastSyncUtc { get; set; }

    public Company? Company { get; set; }
    public WhatsAppAccount? WhatsAppAccount { get; set; }
    public ICollection<Message> Messages { get; set; } = [];
}
