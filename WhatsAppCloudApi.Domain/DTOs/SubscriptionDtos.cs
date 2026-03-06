namespace WhatsAppCloudApi.Domain.DTOs;

public sealed class CompanySubscriptionUpsertRequest
{
    public int SubscriptionPlanId { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public DateTime? TrialStartDate { get; set; }
    public DateTime? TrialEndDate { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
}
