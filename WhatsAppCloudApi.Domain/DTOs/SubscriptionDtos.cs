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

public sealed class SubscriptionUsageSnapshotDto
{
    public bool IsSubscriptionActive { get; set; }
    public string SubscriptionStatus { get; set; } = "EXPIRED";
    public int? CompanySubscriptionId { get; set; }
    public string? PlanCode { get; set; }
    public string? PlanName { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }

    public int MaxMessagesPerMonth { get; set; }
    public int MessagesUsedThisMonth { get; set; }
    public int RemainingMessagesThisMonth { get; set; }
    public bool IsMessageLimitReached { get; set; }

    public int MaxWhatsAppAccounts { get; set; }
    public int ActiveWhatsAppAccounts { get; set; }
    public int RemainingWhatsAppAccounts { get; set; }
    public bool IsWhatsAppAccountLimitReached { get; set; }

    public int MaxPhoneNumbers { get; set; }
    public int ActivePhoneNumbers { get; set; }
    public int RemainingPhoneNumbers { get; set; }
    public bool IsPhoneNumberLimitReached { get; set; }
}
