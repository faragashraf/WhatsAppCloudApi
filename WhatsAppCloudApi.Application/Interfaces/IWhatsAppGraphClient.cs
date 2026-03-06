using WhatsAppCloudApi.Domain.Models;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Application.Interfaces;

public interface IWhatsAppGraphClient
{
    Task<ApiResponse<GenericGraphResponse>> SendAsync(
        TenantWhatsAppConfig config,
        HttpMethod method,
        string path,
        HttpContent? content,
        CancellationToken cancellationToken = default);
}
