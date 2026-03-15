using WhatsAppCloudApi.Domain.DTOs;

namespace WhatsAppCloudApi.Application.Interfaces;

public interface ISubscriptionValidationService
{
    Task ValidateCanSendMessageAsync(int companyId, CancellationToken cancellationToken = default);
    Task ValidateCanSendMessagesAsync(int companyId, int requestedMessageCount, CancellationToken cancellationToken = default);
    Task ValidateWhatsAppAccountLimitAsync(int companyId, CancellationToken cancellationToken = default);
    Task ValidatePhoneNumberLimitAsync(int companyId, CancellationToken cancellationToken = default);
    Task<SubscriptionUsageSnapshotDto> GetUsageSnapshotAsync(int companyId, CancellationToken cancellationToken = default);
}
