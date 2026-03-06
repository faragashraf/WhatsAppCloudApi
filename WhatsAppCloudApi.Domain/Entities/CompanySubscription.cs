namespace WhatsAppCloudApi.Domain.Entities;

public sealed class CompanySubscription
{
    public int CompanySubscriptionId { get; set; }
    public int CompanyId { get; set; }
    public int SubscriptionPlanId { get; set; }
    public string Status { get; set; } = "TRIAL";
    public DateTime? TrialStartDate { get; set; }
    public DateTime? TrialEndDate { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public Company? Company { get; set; }
    public SubscriptionPlan? SubscriptionPlan { get; set; }
}
