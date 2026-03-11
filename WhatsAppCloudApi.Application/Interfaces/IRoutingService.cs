using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Domain.Models;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Application.Interfaces;

public interface IRoutingService
{
    Task<CompanyRoutingSettings> GetOrCreateCompanySettingsAsync(int companyId, CancellationToken cancellationToken = default);
    Task<ApiResponse<CompanyRoutingSettingsDto>> GetCompanySettingsAsync(int companyId, CancellationToken cancellationToken = default);
    Task<ApiResponse<CompanyRoutingSettingsDto>> UpdateCompanySettingsAsync(int companyId, UpdateCompanyRoutingSettingsRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<CompanyUserRoutingSettingsDto>>> GetCompanyUserRoutingSettingsAsync(int companyId, CancellationToken cancellationToken = default);
    Task<ApiResponse<CompanyUserRoutingSettingsDto>> UpdateCompanyUserRoutingSettingsAsync(int companyId, int companyUserId, UpdateCompanyUserRoutingSettingsRequest request, CancellationToken cancellationToken = default);
    Task<List<CompanyUser>> GetEligibleUsersAsync(int companyId, bool forAutoAssignment, CancellationToken cancellationToken = default);
    Task<AssignmentChangeResult> AssignConversationAsync(
        int companyId,
        Conversation conversation,
        int? newAssignedUserId,
        int? changedByUserId,
        bool updateContactOwner,
        string assignmentMode,
        string reason,
        string? notes,
        CancellationToken cancellationToken = default);
    Task<AssignmentChangeResult> AutoAssignConversationAsync(
        int companyId,
        Conversation conversation,
        Contact contact,
        string reason,
        CancellationToken cancellationToken = default);
}
