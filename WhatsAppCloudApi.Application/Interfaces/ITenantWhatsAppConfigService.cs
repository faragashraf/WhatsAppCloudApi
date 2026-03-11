using WhatsAppCloudApi.Domain.Models;

namespace WhatsAppCloudApi.Application.Interfaces;

public interface ITenantWhatsAppConfigService
{
    Task<TenantWhatsAppConfig> GetRequiredConfigAsync(int companyId, CancellationToken cancellationToken = default);
    Task<TenantWhatsAppConfig?> GetConfigByWhatsAppPhoneNumberIdAsync(int whatsAppPhoneNumberId, CancellationToken cancellationToken = default);
    Task<TenantWhatsAppConfig?> GetConfigByVerifyTokenAsync(string verifyToken, CancellationToken cancellationToken = default);
    Task<TenantWhatsAppConfig?> GetConfigByPhoneNumberIdAsync(string phoneNumberId, CancellationToken cancellationToken = default);
}
