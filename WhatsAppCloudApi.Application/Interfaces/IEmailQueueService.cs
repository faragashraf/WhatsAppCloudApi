using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Application.Interfaces;

public interface IEmailQueueService
{
    Task<ApiResponse<EmailQueueDashboardDto>> GetDashboardAsync(int? companyId, string scope, CancellationToken ct = default);
    Task<ApiResponse<PagedResult<EmailQueueItemDto>>> GetQueueAsync(int? companyId, string scope, EmailQueueQueryParams query, CancellationToken ct = default);
    Task<ApiResponse<EmailQueueItemDto>> QueueEmailAsync(int? companyId, int currentUserId, string scope, QueueEmailRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> CancelQueueItemAsync(int? companyId, string scope, long queueItemId, CancellationToken ct = default);
    Task<ApiResponse<bool>> RetryQueueItemAsync(int? companyId, string scope, long queueItemId, CancellationToken ct = default);
    Task<ApiResponse<IReadOnlyList<EmailAccountDto>>> GetAccountsAsync(int? companyId, string scope, CancellationToken ct = default);
    Task<ApiResponse<EmailAccountDto>> UpsertAccountAsync(int? companyId, int currentUserId, string scope, int? accountId, EmailAccountUpsertRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteAccountAsync(int? companyId, string scope, int accountId, CancellationToken ct = default);
    Task<ApiResponse<IReadOnlyList<EmailNotificationRuleDto>>> GetRulesAsync(int? companyId, string scope, CancellationToken ct = default);
    Task<ApiResponse<EmailNotificationRuleDto>> UpsertRuleAsync(int? companyId, int currentUserId, string scope, long? ruleId, EmailNotificationRuleUpsertRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteRuleAsync(int? companyId, string scope, long ruleId, CancellationToken ct = default);
}
