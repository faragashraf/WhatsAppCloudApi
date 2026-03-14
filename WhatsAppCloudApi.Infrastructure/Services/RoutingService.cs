using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Domain.Models;
using WhatsAppCloudApi.Infrastructure.Data;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Infrastructure.Services;

public sealed class RoutingService : IRoutingService
{
    private static readonly HashSet<string> AllowedAssignmentModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "MANUAL",
        "AUTO"
    };

    private static readonly HashSet<string> AllowedAutoStrategies = new(StringComparer.OrdinalIgnoreCase)
    {
        "ROUND_ROBIN"
    };

    private readonly ApplicationDbContext _dbContext;
    private readonly INotificationService _notificationService;
    private readonly ILogger<RoutingService> _logger;

    public RoutingService(
        ApplicationDbContext dbContext,
        INotificationService notificationService,
        ILogger<RoutingService> logger)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<CompanyRoutingSettings> GetOrCreateCompanySettingsAsync(int companyId, CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.CompanyRoutingSettings
            .FirstOrDefaultAsync(x => x.CompanyId == companyId, cancellationToken);

        if (settings is not null)
        {
            return settings;
        }

        settings = new CompanyRoutingSettings
        {
            CompanyId = companyId,
            AssignmentMode = "MANUAL",
            AutoAssignmentStrategy = "ROUND_ROBIN",
            RespectExistingContactOwner = true,
            ReassignWhenOwnerInactive = true,
            ManualReassignmentUpdatesContactOwner = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.CompanyRoutingSettings.Add(settings);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return settings;
    }

    public async Task<ApiResponse<CompanyRoutingSettingsDto>> GetCompanySettingsAsync(int companyId, CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateCompanySettingsAsync(companyId, cancellationToken);
        return ApiResponse<CompanyRoutingSettingsDto>.Ok(ToDto(settings));
    }

    public async Task<ApiResponse<CompanyRoutingSettingsDto>> UpdateCompanySettingsAsync(int companyId, UpdateCompanyRoutingSettingsRequest request, CancellationToken cancellationToken = default)
    {
        var assignmentMode = request.AssignmentMode.Trim().ToUpperInvariant();
        var autoStrategy = request.AutoAssignmentStrategy.Trim().ToUpperInvariant();

        if (!AllowedAssignmentModes.Contains(assignmentMode))
        {
            return ApiResponse<CompanyRoutingSettingsDto>.Fail("Invalid assignment mode.", HttpStatusCode.BadRequest);
        }

        if (!AllowedAutoStrategies.Contains(autoStrategy))
        {
            return ApiResponse<CompanyRoutingSettingsDto>.Fail("Invalid auto-assignment strategy.", HttpStatusCode.BadRequest);
        }

        var settings = await GetOrCreateCompanySettingsAsync(companyId, cancellationToken);
        settings.AssignmentMode = assignmentMode;
        settings.AutoAssignmentStrategy = autoStrategy;
        settings.RespectExistingContactOwner = request.RespectExistingContactOwner;
        settings.ReassignWhenOwnerInactive = request.ReassignWhenOwnerInactive;
        settings.ManualReassignmentUpdatesContactOwner = request.ManualReassignmentUpdatesContactOwner;
        settings.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ApiResponse<CompanyRoutingSettingsDto>.Ok(ToDto(settings));
    }

    public async Task<ApiResponse<List<CompanyUserRoutingSettingsDto>>> GetCompanyUserRoutingSettingsAsync(int companyId, CancellationToken cancellationToken = default)
    {
        await EnsureCompanyUserRoutingRowsAsync(companyId, cancellationToken);

        var rows = await _dbContext.CompanyUsers
            .AsNoTracking()
            .Include(x => x.RoutingSettings)
            .Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.FullName)
            .ToListAsync(cancellationToken);

        return ApiResponse<List<CompanyUserRoutingSettingsDto>>.Ok(rows.Select(ToDto).ToList());
    }

    public async Task<ApiResponse<CompanyUserRoutingSettingsDto>> UpdateCompanyUserRoutingSettingsAsync(int companyId, int companyUserId, UpdateCompanyUserRoutingSettingsRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.CompanyUsers
            .Include(x => x.RoutingSettings)
            .FirstOrDefaultAsync(x => x.CompanyUserId == companyUserId && x.CompanyId == companyId, cancellationToken);

        if (user is null)
        {
            return ApiResponse<CompanyUserRoutingSettingsDto>.Fail("User not found.", HttpStatusCode.NotFound);
        }

        var routingSettings = await EnsureUserRoutingSettingsAsync(companyId, companyUserId, cancellationToken);
        routingSettings.CanReceiveManualAssignments = request.CanReceiveManualAssignments;
        routingSettings.CanReceiveAutoAssignments = request.CanReceiveAutoAssignments;
        routingSettings.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        user.RoutingSettings = routingSettings;
        return ApiResponse<CompanyUserRoutingSettingsDto>.Ok(ToDto(user));
    }

    public async Task<ApiResponse<List<RoutingTeamDto>>> GetTeamsAsync(int companyId, CancellationToken cancellationToken = default)
    {
        await EnsureCompanyUserRoutingRowsAsync(companyId, cancellationToken);

        var teams = await _dbContext.RoutingTeams
            .AsNoTracking()
            .Include(x => x.Members)
                .ThenInclude(x => x.CompanyUser)
                    .ThenInclude(x => x!.RoutingSettings)
            .Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return ApiResponse<List<RoutingTeamDto>>.Ok(teams.Select(ToTeamDto).ToList());
    }

    public async Task<ApiResponse<RoutingTeamDto>> CreateTeamAsync(int companyId, CreateRoutingTeamRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return ApiResponse<RoutingTeamDto>.Fail("Team name is required.", HttpStatusCode.BadRequest);
        }

        var normalizedName = name.ToLowerInvariant();
        var nameExists = await _dbContext.RoutingTeams
            .AnyAsync(x => x.CompanyId == companyId && x.Name.ToLower() == normalizedName, cancellationToken);
        if (nameExists)
        {
            return ApiResponse<RoutingTeamDto>.Fail("Team name already exists.", HttpStatusCode.Conflict);
        }

        var team = new RoutingTeam
        {
            CompanyId = companyId,
            Name = name,
            Description = NormalizeNullable(request.Description),
            IsActive = request.IsActive,
            AutoAssignmentEnabled = request.AutoAssignmentEnabled,
            ManualAssignmentEnabled = request.ManualAssignmentEnabled,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.RoutingTeams.Add(team);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var membersResult = await UpdateTeamMembersInternalAsync(companyId, team, request.MemberUserIds, cancellationToken);
        if (!membersResult.Success)
        {
            return ApiResponse<RoutingTeamDto>.Fail(
                membersResult.Message ?? "Failed to update team members.",
                membersResult.Error?.StatusCode is int statusCode ? (HttpStatusCode)statusCode : HttpStatusCode.BadRequest,
                details: membersResult.Error?.Details);
        }

        var refreshed = await LoadTeamWithMembersAsync(companyId, team.RoutingTeamId, cancellationToken);
        return ApiResponse<RoutingTeamDto>.Ok(ToTeamDto(refreshed));
    }

    public async Task<ApiResponse<RoutingTeamDto>> UpdateTeamAsync(int companyId, int routingTeamId, UpdateRoutingTeamRequest request, CancellationToken cancellationToken = default)
    {
        var team = await _dbContext.RoutingTeams
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.RoutingTeamId == routingTeamId, cancellationToken);
        if (team is null)
        {
            return ApiResponse<RoutingTeamDto>.Fail("Team not found.", HttpStatusCode.NotFound);
        }

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return ApiResponse<RoutingTeamDto>.Fail("Team name is required.", HttpStatusCode.BadRequest);
        }

        var normalizedName = name.ToLowerInvariant();
        var nameExists = await _dbContext.RoutingTeams
            .AnyAsync(x => x.CompanyId == companyId && x.RoutingTeamId != routingTeamId && x.Name.ToLower() == normalizedName, cancellationToken);
        if (nameExists)
        {
            return ApiResponse<RoutingTeamDto>.Fail("Team name already exists.", HttpStatusCode.Conflict);
        }

        team.Name = name;
        team.Description = NormalizeNullable(request.Description);
        team.IsActive = request.IsActive;
        team.AutoAssignmentEnabled = request.AutoAssignmentEnabled;
        team.ManualAssignmentEnabled = request.ManualAssignmentEnabled;
        team.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var refreshed = await LoadTeamWithMembersAsync(companyId, routingTeamId, cancellationToken);
        return ApiResponse<RoutingTeamDto>.Ok(ToTeamDto(refreshed));
    }

    public async Task<ApiResponse<List<RoutingTeamMemberDto>>> UpdateTeamMembersAsync(int companyId, int routingTeamId, UpdateRoutingTeamMembersRequest request, CancellationToken cancellationToken = default)
    {
        var team = await _dbContext.RoutingTeams
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.RoutingTeamId == routingTeamId, cancellationToken);
        if (team is null)
        {
            return ApiResponse<List<RoutingTeamMemberDto>>.Fail("Team not found.", HttpStatusCode.NotFound);
        }

        return await UpdateTeamMembersInternalAsync(companyId, team, request.MemberUserIds, cancellationToken);
    }

    public async Task<ApiResponse<bool>> DeleteTeamAsync(int companyId, int routingTeamId, CancellationToken cancellationToken = default)
    {
        var team = await _dbContext.RoutingTeams
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.RoutingTeamId == routingTeamId, cancellationToken);
        if (team is null)
        {
            return ApiResponse<bool>.Fail("Team not found.", HttpStatusCode.NotFound);
        }

        var members = await _dbContext.RoutingTeamMembers
            .Where(x => x.CompanyId == companyId && x.RoutingTeamId == routingTeamId)
            .ToListAsync(cancellationToken);
        if (members.Count > 0)
        {
            _dbContext.RoutingTeamMembers.RemoveRange(members);
        }

        var linkedDepartments = await _dbContext.LeadDepartments
            .Where(x => x.CompanyId == companyId && x.RoutingTeamId == routingTeamId)
            .ToListAsync(cancellationToken);
        foreach (var department in linkedDepartments)
        {
            department.RoutingTeamId = null;
            department.UpdatedAtUtc = DateTime.UtcNow;
        }

        var teamConversations = await _dbContext.Conversations
            .Where(x => x.CompanyId == companyId && x.AssignedTeamId == routingTeamId)
            .ToListAsync(cancellationToken);
        foreach (var conversation in teamConversations)
        {
            conversation.AssignedTeamId = null;
            conversation.UpdatedAtUtc = DateTime.UtcNow;
        }

        var assignmentHistoryRows = await _dbContext.ConversationAssignmentHistory
            .Where(x => x.CompanyId == companyId && (x.PreviousAssignedTeamId == routingTeamId || x.NewAssignedTeamId == routingTeamId))
            .ToListAsync(cancellationToken);
        foreach (var history in assignmentHistoryRows)
        {
            if (history.PreviousAssignedTeamId == routingTeamId)
            {
                history.PreviousAssignedTeamId = null;
            }

            if (history.NewAssignedTeamId == routingTeamId)
            {
                history.NewAssignedTeamId = null;
            }
        }

        _dbContext.RoutingTeams.Remove(team);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ApiResponse<bool>.Ok(true);
    }

    public async Task<List<CompanyUser>> GetEligibleUsersAsync(int companyId, bool forAutoAssignment, CancellationToken cancellationToken = default)
    {
        await EnsureCompanyUserRoutingRowsAsync(companyId, cancellationToken);

        var users = await _dbContext.CompanyUsers
            .Include(x => x.RoutingSettings)
            .Where(x => x.CompanyId == companyId && x.IsActive)
            .OrderBy(x => x.FullName)
            .ToListAsync(cancellationToken);

        return users
            .Where(x => IsUserEligibleForAssignment(x, forAutoAssignment))
            .ToList();
    }

    public async Task<List<RoutingTeamMember>> GetEligibleTeamMembersAsync(int companyId, int routingTeamId, bool forAutoAssignment, CancellationToken cancellationToken = default)
    {
        await EnsureCompanyUserRoutingRowsAsync(companyId, cancellationToken);

        var team = await _dbContext.RoutingTeams
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.RoutingTeamId == routingTeamId, cancellationToken);
        if (team is null)
        {
            return [];
        }

        if (forAutoAssignment && (!team.IsActive || !team.AutoAssignmentEnabled))
        {
            return [];
        }

        if (!forAutoAssignment && (!team.IsActive || !team.ManualAssignmentEnabled))
        {
            return [];
        }

        var members = await _dbContext.RoutingTeamMembers
            .Include(x => x.CompanyUser)
                .ThenInclude(x => x!.RoutingSettings)
            .Where(x => x.CompanyId == companyId
                && x.RoutingTeamId == routingTeamId
                && x.IsActive
                && x.CompanyUser != null
                && x.CompanyUser.IsActive)
            .OrderBy(x => x.CompanyUserId)
            .ToListAsync(cancellationToken);

        return members
            .Where(x => x.CompanyUser is not null && IsUserEligibleForAssignment(x.CompanyUser, forAutoAssignment))
            .ToList();
    }

    public async Task<AssignmentChangeResult> AssignConversationAsync(
        int companyId,
        Conversation conversation,
        int? newAssignedTeamId,
        int? newAssignedUserId,
        int? changedByUserId,
        bool updateContactOwner,
        string assignmentMode,
        string reason,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var contact = await _dbContext.Contacts
            .FirstOrDefaultAsync(x => x.ContactId == conversation.ContactId && x.CompanyId == companyId, cancellationToken)
            ?? throw new InvalidOperationException("Conversation contact was not found.");

        var isAutoMode = string.Equals(assignmentMode, "AUTO", StringComparison.OrdinalIgnoreCase);

        if (newAssignedTeamId.HasValue)
        {
            var team = await _dbContext.RoutingTeams
                .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.RoutingTeamId == newAssignedTeamId.Value, cancellationToken)
                ?? throw new InvalidOperationException("Team was not found.");

            if (!team.IsActive)
            {
                throw new InvalidOperationException("Team is inactive.");
            }

            if (isAutoMode && !team.AutoAssignmentEnabled)
            {
                throw new InvalidOperationException("Team is not available for automatic assignment.");
            }

            if (!isAutoMode && !team.ManualAssignmentEnabled)
            {
                throw new InvalidOperationException("Team is not available for manual assignment.");
            }
        }

        if (newAssignedUserId.HasValue)
        {
            if (newAssignedTeamId.HasValue)
            {
                var eligibleMembers = await GetEligibleTeamMembersAsync(
                    companyId,
                    newAssignedTeamId.Value,
                    forAutoAssignment: isAutoMode,
                    cancellationToken);

                if (eligibleMembers.All(x => x.CompanyUserId != newAssignedUserId.Value))
                {
                    throw new InvalidOperationException("User is not eligible within the selected team.");
                }
            }
            else
            {
                await EnsureUserIsEligibleAsync(companyId, newAssignedUserId.Value, forAutoAssignment: isAutoMode, cancellationToken);
            }
        }

        var previousAssignedTeamId = conversation.AssignedTeamId;
        var previousAssignedUserId = conversation.AssignedUserId;
        var previousOwnerUserId = contact.OwnerUserId;
        var now = DateTime.UtcNow;

        var assignmentChanged = previousAssignedTeamId != newAssignedTeamId
            || previousAssignedUserId != newAssignedUserId;
        var ownerChanged = false;

        if (assignmentChanged)
        {
            conversation.AssignedTeamId = newAssignedTeamId;
            conversation.AssignedUserId = newAssignedUserId;
            conversation.UpdatedAtUtc = now;
        }

        if (updateContactOwner && contact.OwnerUserId != newAssignedUserId)
        {
            contact.OwnerUserId = newAssignedUserId;
            contact.OwnerAssignedAtUtc = newAssignedUserId.HasValue ? now : null;
            contact.UpdatedAtUtc = now;
            ownerChanged = true;
        }

        if (assignmentChanged || ownerChanged)
        {
            _dbContext.ConversationAssignmentHistory.Add(new ConversationAssignmentHistory
            {
                CompanyId = companyId,
                ConversationId = conversation.ConversationId,
                ContactId = contact.ContactId,
                PreviousAssignedTeamId = previousAssignedTeamId,
                NewAssignedTeamId = newAssignedTeamId,
                PreviousAssignedUserId = previousAssignedUserId,
                NewAssignedUserId = newAssignedUserId,
                PreviousOwnerUserId = previousOwnerUserId,
                NewOwnerUserId = contact.OwnerUserId,
                AssignmentMode = assignmentMode,
                Reason = string.IsNullOrWhiteSpace(reason) ? "MANUAL_ASSIGN" : reason.Trim().ToUpperInvariant(),
                ChangedByUserId = changedByUserId,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                ChangedAtUtc = now
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new AssignmentChangeResult
        {
            Changed = assignmentChanged || ownerChanged,
            AssignmentChanged = assignmentChanged,
            OwnerChanged = ownerChanged,
            Conversation = conversation,
            Contact = contact
        };
    }

    public async Task<AssignmentChangeResult> AutoAssignConversationAsync(
        int companyId,
        Conversation conversation,
        Contact contact,
        string reason,
        int? preferredTeamId = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateCompanySettingsAsync(companyId, cancellationToken);
        if (!preferredTeamId.HasValue
            && !string.Equals(settings.AssignmentMode, "AUTO", StringComparison.OrdinalIgnoreCase))
        {
            return new AssignmentChangeResult
            {
                Changed = false,
                Conversation = conversation,
                Contact = contact
            };
        }

        await EnsureCompanyUserRoutingRowsAsync(companyId, cancellationToken);

        var teams = await _dbContext.RoutingTeams
            .Include(x => x.Members)
                .ThenInclude(x => x.CompanyUser)
                    .ThenInclude(x => x!.RoutingSettings)
            .Where(x => x.CompanyId == companyId && x.IsActive && x.AutoAssignmentEnabled)
            .ToListAsync(cancellationToken);

        if (preferredTeamId.HasValue)
        {
            teams = teams.Where(x => x.RoutingTeamId == preferredTeamId.Value).ToList();
        }

        bool IsAutoEligibleMember(RoutingTeamMember member)
            => member.IsActive
                && member.CompanyUser is not null
                && member.CompanyUser.IsActive
                && IsUserEligibleForAssignment(member.CompanyUser, forAutoAssignment: true);

        RoutingTeamMember? FindEligibleMemberByUserId(int userId)
            => teams
                .SelectMany(x => x.Members)
                .FirstOrDefault(x => x.CompanyUserId == userId && IsAutoEligibleMember(x));

        var currentAssignedMember = conversation.AssignedUserId.HasValue
            ? FindEligibleMemberByUserId(conversation.AssignedUserId.Value)
            : null;
        if (currentAssignedMember is not null)
        {
            return new AssignmentChangeResult
            {
                Changed = false,
                Conversation = conversation,
                Contact = contact
            };
        }

        var ownerMember = contact.OwnerUserId.HasValue
            ? FindEligibleMemberByUserId(contact.OwnerUserId.Value)
            : null;

        if (settings.RespectExistingContactOwner && ownerMember is not null)
        {
            return await AssignConversationAsync(
                companyId,
                conversation,
                ownerMember.RoutingTeamId,
                ownerMember.CompanyUserId,
                changedByUserId: null,
                updateContactOwner: false,
                assignmentMode: "AUTO",
                reason: string.IsNullOrWhiteSpace(reason) ? "AUTO_OWNER_REUSE" : reason,
                notes: "Reused the existing active contact owner.",
                cancellationToken: cancellationToken);
        }

        if (settings.RespectExistingContactOwner
            && contact.OwnerUserId.HasValue
            && ownerMember is null
            && !settings.ReassignWhenOwnerInactive)
        {
            if (conversation.AssignedUserId.HasValue || conversation.AssignedTeamId.HasValue)
            {
                await AssignConversationAsync(
                    companyId,
                    conversation,
                    newAssignedTeamId: null,
                    newAssignedUserId: null,
                    changedByUserId: null,
                    updateContactOwner: false,
                    assignmentMode: "AUTO",
                    reason: "AUTO_OWNER_INACTIVE",
                    notes: "Cleared assignment because contact owner is inactive and automatic owner transfer is disabled.",
                    cancellationToken: cancellationToken);
            }

            await NotifyNoAvailableAgentAsync(companyId, conversation, "Contact owner is inactive and automatic owner transfer is disabled.", cancellationToken);
            return new AssignmentChangeResult
            {
                Changed = false,
                NoAvailableAgent = true,
                Conversation = conversation,
                Contact = contact
            };
        }

        var teamCandidate = teams
            .Select(team => new
            {
                Team = team,
                EligibleMembers = team.Members
                    .Where(IsAutoEligibleMember)
                    .OrderBy(member => member.LastAutoAssignedAtUtc ?? DateTime.MinValue)
                    .ThenBy(member => member.CompanyUserId)
                    .ToList()
            })
            .Where(x => x.EligibleMembers.Count > 0)
            .OrderBy(x => x.Team.LastAutoAssignedAtUtc ?? DateTime.MinValue)
            .ThenBy(x => x.Team.RoutingTeamId)
            .FirstOrDefault();

        if (teamCandidate is null)
        {
            if (conversation.AssignedUserId.HasValue || conversation.AssignedTeamId.HasValue)
            {
                await AssignConversationAsync(
                    companyId,
                    conversation,
                    newAssignedTeamId: null,
                    newAssignedUserId: null,
                    changedByUserId: null,
                    updateContactOwner: false,
                    assignmentMode: "AUTO",
                    reason: preferredTeamId.HasValue ? "AUTO_TEAM_NO_AVAILABLE_MEMBER" : "AUTO_NO_AVAILABLE_AGENT",
                    notes: "Cleared assignment because no auto-routing candidate is available.",
                    cancellationToken: cancellationToken);
            }

            await NotifyNoAvailableAgentAsync(
                companyId,
                conversation,
                preferredTeamId.HasValue
                    ? "No active auto-assignment team member is available in the selected team."
                    : "No active auto-assignment team member is currently available.",
                cancellationToken);

            return new AssignmentChangeResult
            {
                Changed = false,
                NoAvailableAgent = true,
                Conversation = conversation,
                Contact = contact
            };
        }

        var selectedTeam = teamCandidate.Team;
        var selectedMember = teamCandidate.EligibleMembers[0];
        var now = DateTime.UtcNow;

        selectedTeam.LastAutoAssignedAtUtc = now;
        selectedTeam.UpdatedAtUtc = now;
        selectedMember.LastAutoAssignedAtUtc = now;
        selectedMember.UpdatedAtUtc = now;

        var shouldUpdateOwner = !contact.OwnerUserId.HasValue
            || !settings.RespectExistingContactOwner
            || (ownerMember is null && settings.ReassignWhenOwnerInactive);

        var result = await AssignConversationAsync(
            companyId,
            conversation,
            selectedTeam.RoutingTeamId,
            selectedMember.CompanyUserId,
            changedByUserId: null,
            updateContactOwner: shouldUpdateOwner,
            assignmentMode: "AUTO",
            reason: string.IsNullOrWhiteSpace(reason) ? "AUTO_ASSIGN" : reason,
            notes: shouldUpdateOwner
                ? "Assigned automatically from routing team and updated contact owner."
                : "Assigned automatically from routing team without changing contact owner.",
            cancellationToken: cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    private async Task<ApiResponse<List<RoutingTeamMemberDto>>> UpdateTeamMembersInternalAsync(
        int companyId,
        RoutingTeam team,
        IEnumerable<int>? requestedMemberUserIds,
        CancellationToken cancellationToken)
    {
        await EnsureCompanyUserRoutingRowsAsync(companyId, cancellationToken);

        var distinctUserIds = (requestedMemberUserIds ?? [])
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        var users = await _dbContext.CompanyUsers
            .Where(x => x.CompanyId == companyId && distinctUserIds.Contains(x.CompanyUserId) && x.IsActive)
            .ToListAsync(cancellationToken);

        if (users.Count != distinctUserIds.Count)
        {
            return ApiResponse<List<RoutingTeamMemberDto>>.Fail("One or more selected users are invalid or inactive.", HttpStatusCode.BadRequest);
        }

        var existingMembers = await _dbContext.RoutingTeamMembers
            .Where(x => x.CompanyId == companyId && x.RoutingTeamId == team.RoutingTeamId)
            .ToListAsync(cancellationToken);

        var existingUserIds = existingMembers.Select(x => x.CompanyUserId).ToHashSet();
        var desiredUserIds = distinctUserIds.ToHashSet();

        var toRemove = existingMembers.Where(x => !desiredUserIds.Contains(x.CompanyUserId)).ToList();
        var toKeep = existingMembers.Where(x => desiredUserIds.Contains(x.CompanyUserId)).ToList();

        foreach (var member in toKeep)
        {
            if (!member.IsActive)
            {
                member.IsActive = true;
                member.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        if (toRemove.Count > 0)
        {
            _dbContext.RoutingTeamMembers.RemoveRange(toRemove);
        }

        var createdAt = DateTime.UtcNow;
        var toAdd = desiredUserIds
            .Where(x => !existingUserIds.Contains(x))
            .Select(userId => new RoutingTeamMember
            {
                CompanyId = companyId,
                RoutingTeamId = team.RoutingTeamId,
                CompanyUserId = userId,
                IsActive = true,
                CreatedAtUtc = createdAt
            })
            .ToList();

        if (toAdd.Count > 0)
        {
            _dbContext.RoutingTeamMembers.AddRange(toAdd);
        }

        team.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var refreshedMembers = await _dbContext.RoutingTeamMembers
            .AsNoTracking()
            .Include(x => x.CompanyUser)
                .ThenInclude(x => x!.RoutingSettings)
            .Where(x => x.CompanyId == companyId && x.RoutingTeamId == team.RoutingTeamId)
            .OrderBy(x => x.CompanyUser!.FullName)
            .ToListAsync(cancellationToken);

        var items = refreshedMembers
            .Where(x => x.CompanyUser is not null)
            .Select(ToTeamMemberDto)
            .ToList();

        return ApiResponse<List<RoutingTeamMemberDto>>.Ok(items);
    }

    private async Task<RoutingTeam> LoadTeamWithMembersAsync(int companyId, int routingTeamId, CancellationToken cancellationToken)
    {
        return await _dbContext.RoutingTeams
            .AsNoTracking()
            .Include(x => x.Members)
                .ThenInclude(x => x.CompanyUser)
                    .ThenInclude(x => x!.RoutingSettings)
            .FirstAsync(x => x.CompanyId == companyId && x.RoutingTeamId == routingTeamId, cancellationToken);
    }

    private async Task EnsureCompanyUserRoutingRowsAsync(int companyId, CancellationToken cancellationToken)
    {
        var companyUserIds = await _dbContext.CompanyUsers
            .Where(x => x.CompanyId == companyId)
            .Select(x => x.CompanyUserId)
            .ToListAsync(cancellationToken);

        var existingIds = await _dbContext.CompanyUserRoutingSettings
            .Where(x => x.CompanyId == companyId)
            .Select(x => x.CompanyUserId)
            .ToListAsync(cancellationToken);

        var missingIds = companyUserIds.Except(existingIds).ToList();
        if (missingIds.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        _dbContext.CompanyUserRoutingSettings.AddRange(missingIds.Select(x => new CompanyUserRoutingSettings
        {
            CompanyId = companyId,
            CompanyUserId = x,
            CanReceiveManualAssignments = true,
            CanReceiveAutoAssignments = true,
            CreatedAtUtc = now
        }));

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<CompanyUserRoutingSettings> EnsureUserRoutingSettingsAsync(int companyId, int companyUserId, CancellationToken cancellationToken)
    {
        var row = await _dbContext.CompanyUserRoutingSettings
            .FirstOrDefaultAsync(
                x => x.CompanyId == companyId && x.CompanyUserId == companyUserId,
                cancellationToken);

        if (row is not null)
        {
            return row;
        }

        row = new CompanyUserRoutingSettings
        {
            CompanyId = companyId,
            CompanyUserId = companyUserId,
            CanReceiveManualAssignments = true,
            CanReceiveAutoAssignments = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.CompanyUserRoutingSettings.Add(row);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return row;
    }

    private async Task EnsureUserIsEligibleAsync(int companyId, int companyUserId, bool forAutoAssignment, CancellationToken cancellationToken)
    {
        var users = await GetEligibleUsersAsync(companyId, forAutoAssignment, cancellationToken);
        if (users.Any(x => x.CompanyUserId == companyUserId))
        {
            return;
        }

        throw new InvalidOperationException("User is not eligible for assignment.");
    }

    private bool IsUserEligibleForAssignment(CompanyUser user, bool forAutoAssignment)
    {
        if (!user.EffectivePermissions.ConversationsView || !user.EffectivePermissions.ConversationsSend)
        {
            return false;
        }

        if (user.RoutingSettings is null)
        {
            return true;
        }

        return forAutoAssignment
            ? user.RoutingSettings.CanReceiveAutoAssignments
            : user.RoutingSettings.CanReceiveManualAssignments;
    }

    private async Task NotifyNoAvailableAgentAsync(int companyId, Conversation conversation, string message, CancellationToken cancellationToken)
    {
        try
        {
            await _notificationService.CreateNotificationAsync(companyId, new CreateNotificationRequest
            {
                Type = "warning",
                Title = "Conversation is waiting for assignment",
                Body = message,
                Category = "routing",
                MetadataJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    conversationId = conversation.ConversationId,
                    contactId = conversation.ContactId,
                    contactNumber = conversation.ContactNumber,
                    assignedTeamId = conversation.AssignedTeamId,
                    assignedUserId = conversation.AssignedUserId
                })
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create routing notification for conversation {ConversationId}.", conversation.ConversationId);
        }
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static CompanyRoutingSettingsDto ToDto(CompanyRoutingSettings settings)
        => new()
        {
            CompanyId = settings.CompanyId,
            AssignmentMode = settings.AssignmentMode,
            AutoAssignmentStrategy = settings.AutoAssignmentStrategy,
            RespectExistingContactOwner = settings.RespectExistingContactOwner,
            ReassignWhenOwnerInactive = settings.ReassignWhenOwnerInactive,
            ManualReassignmentUpdatesContactOwner = settings.ManualReassignmentUpdatesContactOwner,
            CreatedAtUtc = settings.CreatedAtUtc,
            UpdatedAtUtc = settings.UpdatedAtUtc
        };

    private static CompanyUserRoutingSettingsDto ToDto(CompanyUser user)
        => new()
        {
            CompanyUserId = user.CompanyUserId,
            CompanyId = user.CompanyId,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role,
            IsActive = user.IsActive,
            CanReceiveManualAssignments = user.RoutingSettings?.CanReceiveManualAssignments ?? true,
            CanReceiveAutoAssignments = user.RoutingSettings?.CanReceiveAutoAssignments ?? true,
            LastAutoAssignedAtUtc = user.RoutingSettings?.LastAutoAssignedAtUtc
        };

    private static RoutingTeamDto ToTeamDto(RoutingTeam team)
    {
        var members = (team.Members ?? [])
            .Where(x => x.CompanyUser is not null)
            .OrderBy(x => x.CompanyUser!.FullName)
            .Select(ToTeamMemberDto)
            .ToList();

        return new RoutingTeamDto
        {
            RoutingTeamId = team.RoutingTeamId,
            CompanyId = team.CompanyId,
            Name = team.Name,
            Description = team.Description,
            IsActive = team.IsActive,
            AutoAssignmentEnabled = team.AutoAssignmentEnabled,
            ManualAssignmentEnabled = team.ManualAssignmentEnabled,
            LastAutoAssignedAtUtc = team.LastAutoAssignedAtUtc,
            CreatedAtUtc = team.CreatedAtUtc,
            UpdatedAtUtc = team.UpdatedAtUtc,
            MemberCount = members.Count,
            Members = members
        };
    }

    private static RoutingTeamMemberDto ToTeamMemberDto(RoutingTeamMember member)
    {
        var user = member.CompanyUser;
        return new RoutingTeamMemberDto
        {
            RoutingTeamMemberId = member.RoutingTeamMemberId,
            RoutingTeamId = member.RoutingTeamId,
            CompanyUserId = member.CompanyUserId,
            FullName = user?.FullName ?? string.Empty,
            Email = user?.Email ?? string.Empty,
            Role = user?.Role ?? string.Empty,
            IsActive = member.IsActive,
            CanReceiveManualAssignments = user?.RoutingSettings?.CanReceiveManualAssignments ?? true,
            CanReceiveAutoAssignments = user?.RoutingSettings?.CanReceiveAutoAssignments ?? true,
            LastAutoAssignedAtUtc = member.LastAutoAssignedAtUtc
        };
    }
}
