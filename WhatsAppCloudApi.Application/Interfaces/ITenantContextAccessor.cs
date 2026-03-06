using WhatsAppCloudApi.Domain.Models;

namespace WhatsAppCloudApi.Application.Interfaces;

public interface ITenantContextAccessor
{
    TenantContext GetRequiredContext();
    bool TryGetContext(out TenantContext? context);
}
