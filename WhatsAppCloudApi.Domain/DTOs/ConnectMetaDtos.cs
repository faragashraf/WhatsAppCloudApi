using System.ComponentModel.DataAnnotations;

namespace WhatsAppCloudApi.Domain.DTOs;

/// <summary>
/// Request body for POST /api/company/connect-meta.
/// </summary>
public sealed class ConnectMetaRequest
{
    [Required(ErrorMessage = "Business Account ID is required.")]
    public string BusinessAccountId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Access Token is required.")]
    public string AccessToken { get; set; } = string.Empty;
}

/// <summary>
/// Response returned after connecting a Meta Business Account.
/// </summary>
public sealed class ConnectMetaResponse
{
    public string Status { get; set; } = string.Empty; // Connected | InvalidToken | AccountNotFound | PermissionDenied | Error
    public string? BusinessAccountName { get; set; }
    public string? BusinessAccountId { get; set; }
    public int PhoneNumbersImported { get; set; }
    public bool WebhookConfigured { get; set; }
    public string? WebhookUrl { get; set; }
    public DateTime? LastSyncUtc { get; set; }
    public string? ErrorMessage { get; set; }
    public List<MetaPhoneNumberInfo> PhoneNumbers { get; set; } = [];
}

/// <summary>
/// Phone number info retrieved from Meta Graph API.
/// </summary>
public sealed class MetaPhoneNumberInfo
{
    public string PhoneNumberId { get; set; } = string.Empty;
    public string DisplayPhoneNumber { get; set; } = string.Empty;
    public string? VerifiedName { get; set; }
    public string? CodeVerificationStatus { get; set; }
    public string? QualityRating { get; set; }
    public string? PlatformType { get; set; }
    public string? ThroughputLevel { get; set; }
    public string? LastOnboardedTime { get; set; }
}

/// <summary>
/// Request body for POST /api/phone-numbers/sync.
/// </summary>
public sealed class PhoneNumberSyncRequest
{
    [Required(ErrorMessage = "Business Account ID is required.")]
    public string BusinessAccountId { get; set; } = string.Empty;

    public List<PhoneNumberSyncItem> PhoneNumbers { get; set; } = [];
}

public sealed class PhoneNumberSyncItem
{
    [Required] public string PhoneNumberId { get; set; } = string.Empty;
    [Required] public string DisplayPhoneNumber { get; set; } = string.Empty;
    public string? VerifiedName { get; set; }
    public string? CodeVerificationStatus { get; set; }
    public string? QualityRating { get; set; }
    public string? PlatformType { get; set; }
    public string? ThroughputLevel { get; set; }
    public string? LastOnboardedTime { get; set; }
}

/// <summary>
/// Response for phone number sync operation.
/// </summary>
public sealed class PhoneNumberSyncResponse
{
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Total { get; set; }
}

/// <summary>
/// Response for verifying the WhatsApp connection status.
/// </summary>
public sealed class WhatsAppConnectionStatus
{
    public bool IsConnected { get; set; }
    public string? BusinessAccountId { get; set; }
    public string? BusinessAccountName { get; set; }
    public string ConnectionStatus { get; set; } = "Disconnected"; // Connected | Disconnected | InvalidToken | Error
    public int PhoneNumberCount { get; set; }
    public DateTime? LastSyncUtc { get; set; }
    public bool TokenValid { get; set; }
    public string? WebhookUrl { get; set; }
    public List<MetaPhoneNumberInfo> PhoneNumbers { get; set; } = [];
}
