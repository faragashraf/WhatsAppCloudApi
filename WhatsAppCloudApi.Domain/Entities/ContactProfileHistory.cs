namespace WhatsAppCloudApi.Domain.Entities;

public sealed class ContactProfileHistory
{
    public long ContactProfileHistoryId { get; set; }
    public int CompanyId { get; set; }
    public long ContactId { get; set; }
    public int? ChangedByUserId { get; set; }
    public string ChangeType { get; set; } = "DETAILS_UPDATED";
    public string FieldName { get; set; } = string.Empty;
    public string? PreviousValue { get; set; }
    public string? NewValue { get; set; }
    public string Source { get; set; } = "system";
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Company? Company { get; set; }
    public Contact? Contact { get; set; }
    public CompanyUser? ChangedByUser { get; set; }
}
