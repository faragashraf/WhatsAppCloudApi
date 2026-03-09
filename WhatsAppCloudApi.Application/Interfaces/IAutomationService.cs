using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Application.Interfaces;

public interface IAutomationService
{
    Task<ApiResponse<List<AutomationRule>>> GetRulesAsync(int companyId, CancellationToken ct = default);
    Task<ApiResponse<AutomationRule>> GetRuleByIdAsync(int companyId, long ruleId, CancellationToken ct = default);
    Task<ApiResponse<AutomationRule>> CreateRuleAsync(int companyId, AutomationRuleUpsertRequest request, CancellationToken ct = default);
    Task<ApiResponse<AutomationRule>> UpdateRuleAsync(int companyId, long ruleId, AutomationRuleUpsertRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteRuleAsync(int companyId, long ruleId, CancellationToken ct = default);
    Task<ApiResponse<bool>> ToggleRuleAsync(int companyId, long ruleId, CancellationToken ct = default);
    Task<AutomationRule?> FindMatchingRuleAsync(int companyId, string incomingText, CancellationToken ct = default);
}
