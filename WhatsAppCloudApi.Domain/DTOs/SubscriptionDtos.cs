using System.ComponentModel.DataAnnotations;

namespace WhatsAppCloudApi.Domain.DTOs;

public sealed class CompanySubscriptionUpsertRequest
{
    [Range(1, int.MaxValue)]
    public int SubscriptionPlanId { get; set; }

    [Required, MaxLength(50)]
    public string Status { get; set; } = "ACTIVE";
    public DateTime? TrialStartDate { get; set; }
    public DateTime? TrialEndDate { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
}
