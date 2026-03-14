using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Application.Interfaces;

public interface ICampaignService
{
    Task<ApiResponse<PagedResult<Campaign>>> GetCampaignsAsync(int companyId, CampaignQueryParams query, CancellationToken ct = default);
    Task<ApiResponse<Campaign>> GetCampaignByIdAsync(int companyId, long campaignId, CancellationToken ct = default);
    Task<ApiResponse<CampaignRecipientValidationResultDto>> ValidateRecipientsAsync(int companyId, CampaignRecipientValidationRequest request, CancellationToken ct = default);
    Task<ApiResponse<Campaign>> CreateCampaignAsync(int companyId, CampaignCreateRequest request, CancellationToken ct = default);
    Task<ApiResponse<Campaign>> UpdateCampaignAsync(int companyId, long campaignId, CampaignUpdateRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> CancelCampaignAsync(int companyId, long campaignId, CancellationToken ct = default);
    Task<ApiResponse<bool>> LaunchCampaignAsync(int companyId, long campaignId, CancellationToken ct = default);
    Task<ApiResponse<PagedResult<CampaignContact>>> GetCampaignContactsAsync(int companyId, long campaignId, int page, int pageSize, CancellationToken ct = default);
}
