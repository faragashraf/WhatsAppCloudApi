namespace WhatsAppCloudApi.Domain.Entities;

public sealed class SubscriptionPlan
{
    public int SubscriptionPlanId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int TrialDays { get; set; }
    public int MaxMessagesPerMonth { get; set; }
    public int MaxWhatsAppAccounts { get; set; }
    /// <summary>
    /// Maximum active phone numbers allowed for this plan.
    /// Use 0 for unlimited.
    /// </summary>
    public int MaxPhoneNumbers { get; set; }
    public decimal? MonthlyPrice { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<CompanySubscription> CompanySubscriptions { get; set; } = [];
}
