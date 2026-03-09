using WhatsAppCloudApi.Domain.DTOs;

namespace WhatsAppCloudApi.Application.Interfaces;

/// <summary>
/// Service that verifies a Meta Business Account, retrieves phone numbers,
/// and manages the WhatsApp connection lifecycle.
/// </summary>
public interface IMetaVerificationService
{
    /// <summary>
    /// Verifies a WhatsApp Business Account with Meta's Graph API,
    /// stores credentials, retrieves phone numbers, and configures webhooks.
    /// </summary>
    Task<ConnectMetaResponse> ConnectBusinessAccountAsync(
        int companyId,
        ConnectMetaRequest request,
        string webhookBaseUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes the connection by re-verifying the token and syncing phone numbers.
    /// </summary>
    Task<WhatsAppConnectionStatus> VerifyConnectionAsync(
        int companyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Syncs phone numbers from request data (upsert: create or update, no duplicates).
    /// </summary>
    Task<PhoneNumberSyncResponse> SyncPhoneNumbersAsync(
        int companyId,
        PhoneNumberSyncRequest request,
        CancellationToken cancellationToken = default);
}
