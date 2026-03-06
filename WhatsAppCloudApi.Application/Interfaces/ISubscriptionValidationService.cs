namespace WhatsAppCloudApi.Application.Interfaces;

public interface ISubscriptionValidationService
{
    Task ValidateCanSendMessageAsync(int companyId, CancellationToken cancellationToken = default);
    Task ValidateWhatsAppAccountLimitAsync(int companyId, CancellationToken cancellationToken = default);
}
