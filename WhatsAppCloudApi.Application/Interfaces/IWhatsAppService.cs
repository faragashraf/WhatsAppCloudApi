using WhatsAppCloudApi.Domain.Models;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Application.Interfaces;

public interface IWhatsAppService
{
    Task<ApiResponse<GenericGraphResponse>> SendTextMessageAsync(SendTextMessageRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<GenericGraphResponse>> SendTemplateMessageAsync(SendTemplateMessageRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<GenericGraphResponse>> SendMediaMessageAsync(SendMediaMessageRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<GenericGraphResponse>> UploadMediaAsync(UploadMediaRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<GenericGraphResponse>> GetMediaUrlAsync(string mediaId, CancellationToken cancellationToken = default);
    Task<ApiResponse<GenericGraphResponse>> DeleteMediaAsync(string mediaId, CancellationToken cancellationToken = default);
    Task<ApiResponse<GenericGraphResponse>> MarkMessageAsReadAsync(MarkAsReadRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<GenericGraphResponse>> GetPhoneNumberDetailsAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<GenericGraphResponse>> RegisterPhoneNumberAsync(RegisterPhoneNumberRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<GenericGraphResponse>> DeregisterPhoneNumberAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<GenericGraphResponse>> RequestVerificationCodeAsync(RequestVerificationCodeRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<GenericGraphResponse>> VerifyCodeAsync(VerifyCodeRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<GenericGraphResponse>> GetMessageTemplatesAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<GenericGraphResponse>> CreateMessageTemplateAsync(CreateTemplateRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<GenericGraphResponse>> DeleteMessageTemplateAsync(string templateId, CancellationToken cancellationToken = default);
    Task<ApiResponse<GenericGraphResponse>> GetBusinessProfileAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<GenericGraphResponse>> UpdateBusinessProfileAsync(UpdateBusinessProfileRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<GenericGraphResponse>> SendGraphRequestAsync(GraphApiRequest request, CancellationToken cancellationToken = default);
}
