using WhatsAppCloudApi.Domain.Models;

namespace WhatsAppCloudApi.Application.Interfaces;

public interface ICustomerConversationResolver
{
    Task<ResolvedConversationContext> ResolveAsync(
        int companyId,
        string phoneNumber,
        int? whatsAppPhoneNumberId,
        string? preferredContactName = null,
        string? source = null,
        CancellationToken cancellationToken = default);
}
