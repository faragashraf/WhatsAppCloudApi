namespace WhatsAppCloudApi.Domain.Entities;

public sealed class Contact
{
    public long ContactId { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Tags { get; set; }
    public string? CustomFields { get; set; }
    public string? Source { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public Company? Company { get; set; }
    public ICollection<Conversation> Conversations { get; set; } = [];
    public ICollection<CampaignContact> CampaignContacts { get; set; } = [];
}
