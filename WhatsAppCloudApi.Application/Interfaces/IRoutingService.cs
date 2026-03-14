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
    Task<ApiResponse<List<RoutingTeamDto>>> GetTeamsAsync(int companyId, CancellationToken cancellationToken = default);
    Task<ApiResponse<RoutingTeamDto>> CreateTeamAsync(int companyId, CreateRoutingTeamRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<RoutingTeamDto>> UpdateTeamAsync(int companyId, int routingTeamId, UpdateRoutingTeamRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<RoutingTeamMemberDto>>> UpdateTeamMembersAsync(int companyId, int routingTeamId, UpdateRoutingTeamMembersRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteTeamAsync(int companyId, int routingTeamId, CancellationToken cancellationToken = default);
    Task<List<CompanyUser>> GetEligibleUsersAsync(int companyId, bool forAutoAssignment, CancellationToken cancellationToken = default);
    Task<List<RoutingTeamMember>> GetEligibleTeamMembersAsync(int companyId, int routingTeamId, bool forAutoAssignment, CancellationToken cancellationToken = default);
    Task<AssignmentChangeResult> AssignConversationAsync(
        int companyId,
        Conversation conversation,
        int? newAssignedTeamId,
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
        int? preferredTeamId = null,
        CancellationToken cancellationToken = default);
}
