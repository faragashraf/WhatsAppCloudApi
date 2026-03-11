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

    public async Task<List<CompanyUser>> GetEligibleUsersAsync(int companyId, bool forAutoAssignment, CancellationToken cancellationToken = default)
    {
        await EnsureCompanyUserRoutingRowsAsync(companyId, cancellationToken);

        var users = await _dbContext.CompanyUsers
            .Include(x => x.RoutingSettings)
            .Where(x => x.CompanyId == companyId && x.IsActive)
            .OrderBy(x => x.FullName)
            .ToListAsync(cancellationToken);

        return users
            .Where(x => x.EffectivePermissions.ConversationsView && x.EffectivePermissions.ConversationsSend)
            .Where(x => x.RoutingSettings is not null
                && (forAutoAssignment
                    ? x.RoutingSettings.CanReceiveAutoAssignments
                    : x.RoutingSettings.CanReceiveManualAssignments))
            .ToList();
    }

    public async Task<AssignmentChangeResult> AssignConversationAsync(
        int companyId,
        Conversation conversation,
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

        if (newAssignedUserId.HasValue)
        {
            await EnsureUserIsEligibleAsync(companyId, newAssignedUserId.Value, forAutoAssignment: false, cancellationToken);
        }

        var previousAssignedUserId = conversation.AssignedUserId;
        var previousOwnerUserId = contact.OwnerUserId;
        var now = DateTime.UtcNow;

        var assignmentChanged = previousAssignedUserId != newAssignedUserId;
        var ownerChanged = false;

        if (assignmentChanged)
        {
            conversation.AssignedUserId = newAssignedUserId;
            conversation.UpdatedAtUtc = now;
        }

        if (updateContactOwner)
        {
            if (contact.OwnerUserId != newAssignedUserId)
            {
                contact.OwnerUserId = newAssignedUserId;
                contact.OwnerAssignedAtUtc = newAssignedUserId.HasValue ? now : null;
                contact.UpdatedAtUtc = now;
                ownerChanged = true;
            }
        }

        if (assignmentChanged || ownerChanged)
        {
            _dbContext.ConversationAssignmentHistory.Add(new ConversationAssignmentHistory
            {
                CompanyId = companyId,
                ConversationId = conversation.ConversationId,
                ContactId = contact.ContactId,
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
        CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateCompanySettingsAsync(companyId, cancellationToken);
        if (!string.Equals(settings.AssignmentMode, "AUTO", StringComparison.OrdinalIgnoreCase))
        {
            return new AssignmentChangeResult
            {
                Changed = false,
                Conversation = conversation,
                Contact = contact
            };
        }

        await EnsureCompanyUserRoutingRowsAsync(companyId, cancellationToken);

        var activeUsers = await _dbContext.CompanyUsers
            .Include(x => x.RoutingSettings)
            .Where(x => x.CompanyId == companyId && x.IsActive)
            .ToListAsync(cancellationToken);

        bool IsAutoEligible(CompanyUser user)
            => user.EffectivePermissions.ConversationsView
                && user.EffectivePermissions.ConversationsSend
                && user.RoutingSettings?.CanReceiveAutoAssignments == true;

        var currentAssigned = activeUsers.FirstOrDefault(x => x.CompanyUserId == conversation.AssignedUserId);
        if (currentAssigned is not null && IsAutoEligible(currentAssigned))
        {
            return new AssignmentChangeResult
            {
                Changed = false,
                Conversation = conversation,
                Contact = contact
            };
        }

        var currentOwner = activeUsers.FirstOrDefault(x => x.CompanyUserId == contact.OwnerUserId);
        if (settings.RespectExistingContactOwner && currentOwner is not null && IsAutoEligible(currentOwner))
        {
            return await AssignConversationAsync(
                companyId,
                conversation,
                currentOwner.CompanyUserId,
                changedByUserId: null,
                updateContactOwner: false,
                assignmentMode: "AUTO",
                reason: string.IsNullOrWhiteSpace(reason) ? "AUTO_OWNER_REUSE" : reason,
                notes: "Reused the existing active contact owner.",
                cancellationToken: cancellationToken);
        }

        if (settings.RespectExistingContactOwner
            && contact.OwnerUserId.HasValue
            && currentOwner is null
            && !settings.ReassignWhenOwnerInactive)
        {
            if (conversation.AssignedUserId.HasValue)
            {
                await AssignConversationAsync(
                    companyId,
                    conversation,
                    null,
                    changedByUserId: null,
                    updateContactOwner: false,
                    assignmentMode: "AUTO",
                    reason: "AUTO_OWNER_INACTIVE",
                    notes: "Cleared the live assignee because the contact owner is inactive.",
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

        var candidate = activeUsers
            .Where(IsAutoEligible)
            .OrderBy(x => x.RoutingSettings?.LastAutoAssignedAtUtc ?? DateTime.MinValue)
            .ThenBy(x => x.CompanyUserId)
            .FirstOrDefault();

        if (candidate is null)
        {
            if (conversation.AssignedUserId.HasValue)
            {
                await AssignConversationAsync(
                    companyId,
                    conversation,
                    null,
                    changedByUserId: null,
                    updateContactOwner: false,
                    assignmentMode: "AUTO",
                    reason: "AUTO_NO_AVAILABLE_AGENT",
                    notes: "Cleared the live assignee because no auto-routing candidate is available.",
                    cancellationToken: cancellationToken);
            }

            await NotifyNoAvailableAgentAsync(companyId, conversation, "No active auto-assignment user is currently available.", cancellationToken);
            return new AssignmentChangeResult
            {
                Changed = false,
                NoAvailableAgent = true,
                Conversation = conversation,
                Contact = contact
            };
        }

        candidate.RoutingSettings!.LastAutoAssignedAtUtc = DateTime.UtcNow;
        candidate.RoutingSettings.UpdatedAtUtc = DateTime.UtcNow;

        var shouldUpdateOwner = !contact.OwnerUserId.HasValue
            || !settings.RespectExistingContactOwner
            || (currentOwner is null && settings.ReassignWhenOwnerInactive);

        var result = await AssignConversationAsync(
            companyId,
            conversation,
            candidate.CompanyUserId,
            changedByUserId: null,
            updateContactOwner: shouldUpdateOwner,
            assignmentMode: "AUTO",
            reason: string.IsNullOrWhiteSpace(reason) ? "AUTO_ASSIGN" : reason,
            notes: shouldUpdateOwner
                ? "Assigned automatically and updated the contact owner."
                : "Assigned automatically without changing the contact owner.",
            cancellationToken: cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return result;
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
                    contactNumber = conversation.ContactNumber
                })
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create routing notification for conversation {ConversationId}.", conversation.ConversationId);
        }
    }

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
}
