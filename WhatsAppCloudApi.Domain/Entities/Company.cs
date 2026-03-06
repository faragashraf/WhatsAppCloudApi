namespace WhatsAppCloudApi.Domain.Entities;

public sealed class Company
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Status { get; set; } = "ACTIVE";
    public DateTime? CreatedAt { get; set; }
    public DateTime? TrialStartDate { get; set; }
    public DateTime? TrialEndDate { get; set; }
    public DateTime? SubscriptionEndDate { get; set; }

    public ICollection<CompanyUser> Users { get; set; } = [];
    public ICollection<CompanySubscription> Subscriptions { get; set; } = [];
    public ICollection<MetaBusinessAccount> MetaBusinessAccounts { get; set; } = [];
    public ICollection<WhatsAppAccount> WhatsAppAccounts { get; set; } = [];
    public ICollection<WhatsAppPhoneNumber> WhatsAppPhoneNumbers { get; set; } = [];
    public ICollection<Message> Messages { get; set; } = [];
    public ICollection<MessageQueueItem> MessageQueueItems { get; set; } = [];
    public ICollection<ApiLog> ApiLogs { get; set; } = [];
}
