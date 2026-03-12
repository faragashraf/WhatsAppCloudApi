using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Domain.Models;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Application.Interfaces;

public interface IConversationFlowService
{
    Task<ApiResponse<List<ConversationFlowDto>>> GetFlowsAsync(int companyId, CancellationToken ct = default);
    Task<ApiResponse<ConversationFlowDto>> GetFlowByIdAsync(int companyId, long flowId, CancellationToken ct = default);
    Task<ApiResponse<ConversationFlowDto>> CreateFlowAsync(int companyId, ConversationFlowUpsertRequest request, CancellationToken ct = default);
    Task<ApiResponse<ConversationFlowDto>> UpdateFlowAsync(int companyId, long flowId, ConversationFlowUpsertRequest request, CancellationToken ct = default);
    Task<ApiResponse<ConversationFlowDto>> PublishFlowAsync(int companyId, long flowId, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteFlowAsync(int companyId, long flowId, CancellationToken ct = default);
    Task<ApiResponse<bool>> ToggleFlowAsync(int companyId, long flowId, CancellationToken ct = default);
    Task<ConversationFlowRuntimeResult> TryProcessInboundAsync(int companyId, Conversation conversation, Contact contact, FlowInboundMessage inbound, CancellationToken ct = default);
    Task<ConversationFlowRuntimeResult> SimulateFlowAsync(int companyId, long flowId, Conversation conversation, Contact contact, FlowInboundMessage inbound, bool usePublishedVersion, CancellationToken ct = default);
}
