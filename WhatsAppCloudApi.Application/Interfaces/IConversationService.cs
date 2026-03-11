using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Application.Interfaces;

public interface IConversationService
{
    Task<ApiResponse<PagedResult<Conversation>>> GetConversationsAsync(int companyId, ConversationQueryParams query, CancellationToken ct = default);
    Task<ApiResponse<Conversation>> GetConversationByIdAsync(int companyId, long conversationId, CancellationToken ct = default);
    Task<ApiResponse<PagedResult<ConversationMessage>>> GetMessagesAsync(int companyId, long conversationId, ConversationMessageQueryParams query, CancellationToken ct = default);
    Task<ApiResponse<ConversationMessage>> SendMessageAsync(int companyId, long conversationId, SendConversationMessageRequest request, int currentUserId, string currentRole, CancellationToken ct = default);
    Task<ApiResponse<bool>> MarkAsReadAsync(int companyId, long conversationId, CancellationToken ct = default);
    Task<ApiResponse<Conversation>> GetOrCreateConversationAsync(int companyId, string contactNumber, int? phoneNumberId, CancellationToken ct = default);
    Task<ApiResponse<Conversation>> AssignConversationAsync(int companyId, long conversationId, int userId, CancellationToken ct = default);
    Task<ApiResponse<Conversation>> UnassignConversationAsync(int companyId, long conversationId, CancellationToken ct = default);
    Task<ApiResponse<Conversation>> PickConversationAsync(int companyId, long conversationId, int userId, CancellationToken ct = default);
    Task ProcessInboundMessageAsync(int companyId, string contactNumber, string? contactName, int whatsAppPhoneNumberId, string metaMessageId, string messageType, string content, string? mediaUrl, string? mediaMimeType, DateTime? occurredAtUtc = null, CancellationToken ct = default);
    Task ProcessStatusUpdateAsync(string metaMessageId, string status, CancellationToken ct = default);
}
