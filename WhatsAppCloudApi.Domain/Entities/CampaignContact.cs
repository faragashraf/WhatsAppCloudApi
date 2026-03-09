namespace WhatsAppCloudApi.Domain.Entities;

public sealed class CampaignContact
{
    public long CampaignContactId { get; set; }
    public long CampaignId { get; set; }
    public long? ContactId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "PENDING"; // PENDING | SENT | DELIVERED | READ | FAILED
    public string? ExternalMessageId { get; set; }
    public string? FailureReason { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Campaign? Campaign { get; set; }
    public Contact? Contact { get; set; }
}
