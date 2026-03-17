using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Domain.Models;
using WhatsAppCloudApi.Infrastructure.Data;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Infrastructure.Services;

public sealed class ConversationService : IConversationService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan AutomationDuplicateGuardWindow = TimeSpan.FromSeconds(30);
    private static readonly HashSet<string> AllowedMessageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text", "image", "video", "audio", "document", "sticker", "reaction"
    };
    private static readonly HashSet<string> DirectMediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image", "video", "audio", "document", "sticker"
    };

    private readonly ApplicationDbContext _db;
    private readonly ILogger<ConversationService> _logger;
    private readonly ITenantWhatsAppConfigService _configService;
    private readonly IWhatsAppGraphClient _graphClient;
    private readonly INotificationService _notificationService;
    private readonly ICustomerConversationResolver _resolver;
    private readonly IRoutingService _routingService;
    private readonly IConversationFlowService _conversationFlowService;
    private readonly IAutomationService _automationService;
    private readonly IMessageDispatchService _messageDispatchService;

    public ConversationService(
        ApplicationDbContext db,
        ILogger<ConversationService> logger,
        ITenantWhatsAppConfigService configService,
        IWhatsAppGraphClient graphClient,
        INotificationService notificationService,
        ICustomerConversationResolver resolver,
        IRoutingService routingService,
        IConversationFlowService conversationFlowService,
        IAutomationService automationService,
        IMessageDispatchService messageDispatchService)
    {
        _db = db;
        _logger = logger;
        _configService = configService;
        _graphClient = graphClient;
        _notificationService = notificationService;
        _resolver = resolver;
        _routingService = routingService;
        _conversationFlowService = conversationFlowService;
        _automationService = automationService;
        _messageDispatchService = messageDispatchService;
    }

    public async Task<ApiResponse<PagedResult<Conversation>>> GetConversationsAsync(int companyId, ConversationQueryParams query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var q = _db.Conversations
            .Include(c => c.Contact)
                .ThenInclude(c => c!.OwnerUser)
            .Include(c => c.AssignedTeam)
            .Include(c => c.AssignedUser)
            .Include(c => c.WhatsAppPhoneNumber)
            .Where(c => c.CompanyId == companyId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(c => c.ContactNumber.Contains(s) || (c.ContactName != null && c.ContactName.ToLower().Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
            q = q.Where(c => c.Status == query.Status);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(c => c.LastMessageAtUtc ?? c.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return ApiResponse<PagedResult<Conversation>>.Ok(new PagedResult<Conversation>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<ApiResponse<Conversation>> GetConversationByIdAsync(int companyId, long conversationId, CancellationToken ct)
    {
        var conv = await _db.Conversations
            .Include(c => c.Contact)
                .ThenInclude(c => c!.OwnerUser)
            .Include(c => c.AssignedTeam)
            .Include(c => c.AssignedUser)
            .Include(c => c.WhatsAppPhoneNumber)
            .FirstOrDefaultAsync(c => c.CompanyId == companyId && c.ConversationId == conversationId, ct);

        return conv is null
            ? ApiResponse<Conversation>.Fail("Conversation not found", HttpStatusCode.NotFound)
            : ApiResponse<Conversation>.Ok(conv);
    }

    public async Task<ApiResponse<PagedResult<ConversationMessage>>> GetMessagesAsync(int companyId, long conversationId, ConversationMessageQueryParams query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var exists = await _db.Conversations.AnyAsync(c => c.CompanyId == companyId && c.ConversationId == conversationId, ct);
        if (!exists)
            return ApiResponse<PagedResult<ConversationMessage>>.Fail("Conversation not found", HttpStatusCode.NotFound);

        var q = _db.ConversationMessages.Where(m => m.ConversationId == conversationId);

        if (query.Before.HasValue)
            q = q.Where(m => m.TimestampUtc < query.Before.Value);

        if (query.After.HasValue)
            q = q.Where(m => m.TimestampUtc > query.After.Value);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(m => m.TimestampUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return ApiResponse<PagedResult<ConversationMessage>>.Ok(new PagedResult<ConversationMessage>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<ApiResponse<ConversationMessage>> SendMessageAsync(int companyId, long conversationId, SendConversationMessageRequest request, int currentUserId, string currentRole, CancellationToken ct)
    {
        var conv = await _db.Conversations
            .Include(c => c.Contact)
            .FirstOrDefaultAsync(c => c.CompanyId == companyId && c.ConversationId == conversationId, ct);
        if (conv is null)
            return ApiResponse<ConversationMessage>.Fail("Conversation not found", HttpStatusCode.NotFound);

        var messageType = request.MessageType.Trim().ToLowerInvariant();
        if (!AllowedMessageTypes.Contains(messageType))
            return ApiResponse<ConversationMessage>.Fail("Unsupported message type.", HttpStatusCode.BadRequest);

        if (messageType == "text" && string.IsNullOrWhiteSpace(request.Content))
            return ApiResponse<ConversationMessage>.Fail("Text content is required.", HttpStatusCode.BadRequest);

        if (messageType is not ("text" or "reaction") && string.IsNullOrWhiteSpace(request.MediaUrl))
            return ApiResponse<ConversationMessage>.Fail("Media URL or media ID is required.", HttpStatusCode.BadRequest);

        if (messageType == "reaction" && string.IsNullOrWhiteSpace(request.ReplyToMetaMessageId))
            return ApiResponse<ConversationMessage>.Fail("Reaction requires a target message ID.", HttpStatusCode.BadRequest);

        request.Content = (request.Content ?? string.Empty).Trim();
        var conversationContent = string.Equals(messageType, "reaction", StringComparison.OrdinalIgnoreCase)
            ? BuildReactionConversationContent(request.Content, request.ReplyToMetaMessageId)
            : request.Content;

        var interactionValidation = await ValidateConversationInteractionAsync(conv, currentUserId, currentRole, ct);
        if (!interactionValidation.Success)
        {
            return ApiResponse<ConversationMessage>.Fail(
                interactionValidation.Message ?? "Unable to send message.",
                (HttpStatusCode)(interactionValidation.Error?.StatusCode ?? (int)HttpStatusCode.BadRequest),
                details: interactionValidation.Error?.Details);
        }

        if (conv.Contact is null)
        {
            var resolved = await _resolver.ResolveAsync(companyId, conv.ContactNumber, conv.WhatsAppPhoneNumberId, conv.ContactName, "conversation_send", ct);
            conv.Contact = resolved.Contact;
            conv.ContactId = resolved.Contact.ContactId;
            conv.ContactNumber = resolved.NormalizedPhoneNumber;
            conv.ContactName = resolved.Contact.Name;
        }

        var config = await ResolveConfigAsync(companyId, conv.WhatsAppPhoneNumberId, ct);
        var payload = BuildOutboundPayload(conv.ContactNumber, messageType, request);
        var payloadBody = JsonSerializer.Serialize(payload, JsonOpts);
        var now = DateTime.UtcNow;

        var queued = await _messageDispatchService.QueueLinkedMessageAsync(new QueueLinkedMessageRequest
        {
            CompanyId = companyId,
            WhatsAppPhoneNumberId = conv.WhatsAppPhoneNumberId,
            ContactId = conv.ContactId,
            ConversationId = conv.ConversationId,
            CreatedByUserId = currentUserId,
            ToNumber = conv.ContactNumber,
            MessageType = messageType.ToUpperInvariant(),
            MessageBody = payloadBody,
            Source = "INBOX",
            ConversationMessageType = messageType,
            ConversationContent = conversationContent,
            MediaUrl = request.MediaUrl,
            MediaMimeType = request.MediaMimeType,
            FileName = request.FileName,
            ConversationMessageStatus = "sending",
            CreatedAtUtc = now,
            Payload = new MessageQueuePayload
            {
                Method = HttpMethod.Post.Method,
                Path = $"{config.PhoneNumberId}/messages",
                Body = payloadBody
            }
        }, ct);

        if (!string.Equals(messageType, "reaction", StringComparison.OrdinalIgnoreCase))
        {
            var preview = string.IsNullOrWhiteSpace(request.Content) ? $"[{messageType}]" : request.Content;
            conv.LastMessageContent = preview.Length > 1000 ? preview[..1000] : preview;
            conv.LastMessageType = messageType;
            conv.LastMessageAtUtc = now;
        }
        conv.UpdatedAtUtc = now;

        if (conv.Contact is not null)
        {
            conv.Contact.LastSeenAtUtc = now;
            conv.Contact.LastOutboundMessageAtUtc = now;
            conv.Contact.UpdatedAtUtc = now;
        }

        await _db.SaveChangesAsync(ct);
        return ApiResponse<ConversationMessage>.Ok(queued.ConversationMessage);
    }

    public async Task<ApiResponse<ConversationMessage>> SendMediaFileMessageAsync(
        int companyId,
        long conversationId,
        SendConversationMediaFileRequest request,
        int currentUserId,
        string currentRole,
        CancellationToken ct = default)
    {
        if (request.FileData is null || request.FileData.Length == 0)
        {
            return ApiResponse<ConversationMessage>.Fail("File is required.", HttpStatusCode.BadRequest);
        }

        if (request.FileData.Length > 10 * 1024 * 1024)
        {
            return ApiResponse<ConversationMessage>.Fail("File size must not exceed 10MB.", HttpStatusCode.BadRequest);
        }

        var conv = await _db.Conversations
            .Include(c => c.Contact)
            .FirstOrDefaultAsync(c => c.CompanyId == companyId && c.ConversationId == conversationId, ct);
        if (conv is null)
        {
            return ApiResponse<ConversationMessage>.Fail("Conversation not found", HttpStatusCode.NotFound);
        }

        var interactionValidation = await ValidateConversationInteractionAsync(conv, currentUserId, currentRole, ct);
        if (!interactionValidation.Success)
        {
            return ApiResponse<ConversationMessage>.Fail(
                interactionValidation.Message ?? "Unable to send message.",
                (HttpStatusCode)(interactionValidation.Error?.StatusCode ?? (int)HttpStatusCode.BadRequest),
                details: interactionValidation.Error?.Details);
        }

        var messageType = NormalizeConversationMediaType(request.MessageType, request.ContentType, request.FileName);
        if (!DirectMediaTypes.Contains(messageType))
        {
            return ApiResponse<ConversationMessage>.Fail("Unsupported media type.", HttpStatusCode.BadRequest);
        }

        var config = await ResolveConfigAsync(companyId, conv.WhatsAppPhoneNumberId, ct);

        var uploadResult = await UploadConversationMediaAsync(
            config,
            request.FileData,
            request.ContentType,
            request.FileName,
            ct);
        if (!uploadResult.Success)
        {
            return ApiResponse<ConversationMessage>.Fail(
                uploadResult.Message ?? "Failed to upload media.",
                (HttpStatusCode)(uploadResult.Error?.StatusCode ?? (int)HttpStatusCode.BadGateway),
                details: uploadResult.Error?.Details);
        }

        var mediaId = ExtractGraphId(uploadResult.Data);
        if (string.IsNullOrWhiteSpace(mediaId))
        {
            return ApiResponse<ConversationMessage>.Fail(
                "Meta upload did not return a media ID.",
                HttpStatusCode.BadGateway);
        }

        var sendRequest = new SendConversationMessageRequest
        {
            MessageType = messageType,
            Content = string.IsNullOrWhiteSpace(request.Content) ? request.FileName : request.Content,
            MediaUrl = mediaId,
            MediaMimeType = request.ContentType,
            FileName = request.FileName,
            ReplyToMetaMessageId = request.ReplyToMetaMessageId
        };

        return await SendMessageAsync(companyId, conversationId, sendRequest, currentUserId, currentRole, ct);
    }

    public async Task<ApiResponse<ConversationMessage>> ReactToMessageAsync(
        int companyId,
        long conversationId,
        long conversationMessageId,
        ReactToConversationMessageRequest request,
        int currentUserId,
        string currentRole,
        CancellationToken ct = default)
    {
        var targetMessage = await _db.ConversationMessages
            .AsNoTracking()
            .FirstOrDefaultAsync(m =>
                m.CompanyId == companyId
                && m.ConversationId == conversationId
                && m.ConversationMessageId == conversationMessageId, ct);
        if (targetMessage is null)
        {
            return ApiResponse<ConversationMessage>.Fail("Message not found.", HttpStatusCode.NotFound);
        }

        var targetMetaMessageId = string.IsNullOrWhiteSpace(request.ReplyToMetaMessageId)
            ? targetMessage.MetaMessageId
            : request.ReplyToMetaMessageId!.Trim();

        if (string.IsNullOrWhiteSpace(targetMetaMessageId))
        {
            return ApiResponse<ConversationMessage>.Fail(
                "Cannot react to a message without Meta message ID.",
                HttpStatusCode.BadRequest);
        }

        var sendRequest = new SendConversationMessageRequest
        {
            MessageType = "reaction",
            Content = (request.Emoji ?? string.Empty).Trim(),
            ReplyToMetaMessageId = targetMetaMessageId
        };

        return await SendMessageAsync(companyId, conversationId, sendRequest, currentUserId, currentRole, ct);
    }

    public async Task<ApiResponse<ConversationMessage>> ForwardMessageAsync(
        int companyId,
        long sourceConversationId,
        long conversationMessageId,
        ForwardConversationMessageRequest request,
        int currentUserId,
        string currentRole,
        CancellationToken ct = default)
    {
        var sourceMessage = await _db.ConversationMessages
            .AsNoTracking()
            .FirstOrDefaultAsync(m =>
                m.CompanyId == companyId
                && m.ConversationId == sourceConversationId
                && m.ConversationMessageId == conversationMessageId, ct);
        if (sourceMessage is null)
        {
            return ApiResponse<ConversationMessage>.Fail("Message not found.", HttpStatusCode.NotFound);
        }

        if (request.TargetConversationId <= 0)
        {
            return ApiResponse<ConversationMessage>.Fail("Target conversation is required.", HttpStatusCode.BadRequest);
        }

        var sourceType = (sourceMessage.MessageType ?? string.Empty).Trim().ToLowerInvariant();
        SendConversationMessageRequest forwardRequest;

        if (sourceType is "text")
        {
            forwardRequest = new SendConversationMessageRequest
            {
                MessageType = "text",
                Content = sourceMessage.Content ?? string.Empty
            };
        }
        else if (DirectMediaTypes.Contains(sourceType))
        {
            if (string.IsNullOrWhiteSpace(sourceMessage.MediaUrl))
            {
                forwardRequest = new SendConversationMessageRequest
                {
                    MessageType = "text",
                    Content = BuildForwardFallbackText(sourceMessage)
                };
            }
            else
            {
                forwardRequest = new SendConversationMessageRequest
                {
                    MessageType = sourceType,
                    Content = sourceMessage.Content ?? string.Empty,
                    MediaUrl = sourceMessage.MediaUrl,
                    MediaMimeType = sourceMessage.MediaMimeType,
                    FileName = sourceMessage.FileName
                };
            }
        }
        else
        {
            forwardRequest = new SendConversationMessageRequest
            {
                MessageType = "text",
                Content = BuildForwardFallbackText(sourceMessage)
            };
        }

        return await SendMessageAsync(companyId, request.TargetConversationId, forwardRequest, currentUserId, currentRole, ct);
    }

    public async Task<ApiResponse<bool>> MarkAsReadAsync(int companyId, long conversationId, CancellationToken ct)
    {
        var conv = await _db.Conversations.FirstOrDefaultAsync(c => c.CompanyId == companyId && c.ConversationId == conversationId, ct);
        if (conv is null)
            return ApiResponse<bool>.Fail("Conversation not found", HttpStatusCode.NotFound);

        conv.UnreadCount = 0;
        conv.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<bool>> SendTypingIndicatorAsync(int companyId, long conversationId, int currentUserId, string currentRole, CancellationToken ct)
    {
        var conv = await _db.Conversations
            .Include(c => c.WhatsAppPhoneNumber)
            .FirstOrDefaultAsync(c => c.CompanyId == companyId && c.ConversationId == conversationId, ct);
        if (conv is null)
            return ApiResponse<bool>.Fail("Conversation not found", HttpStatusCode.NotFound);

        var interactionValidation = await ValidateConversationInteractionAsync(conv, currentUserId, currentRole, ct);
        if (!interactionValidation.Success)
            return interactionValidation;

        var lastInboundMetaMessageId = await _db.ConversationMessages
            .Where(m => m.ConversationId == conversationId
                && m.Direction == "inbound"
                && !string.IsNullOrWhiteSpace(m.MetaMessageId))
            .OrderByDescending(m => m.TimestampUtc)
            .Select(m => m.MetaMessageId)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(lastInboundMetaMessageId))
        {
            return ApiResponse<bool>.Fail(
                "Typing indicator requires a recent inbound WhatsApp message.",
                HttpStatusCode.BadRequest);
        }

        var config = await ResolveConfigAsync(companyId, conv.WhatsAppPhoneNumberId, ct);
        var payload = new
        {
            messaging_product = "whatsapp",
            status = "read",
            message_id = lastInboundMetaMessageId,
            typing_indicator = new { type = "text" }
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json");
        var result = await _graphClient.SendAsync(config, HttpMethod.Post, $"{config.PhoneNumberId}/messages", content, ct);
        if (!result.Success)
        {
            return ApiResponse<bool>.Fail(
                result.Message ?? "Failed to send typing indicator.",
                (HttpStatusCode)(result.Error?.StatusCode ?? (int)HttpStatusCode.BadGateway),
                details: result.Error?.Details);
        }

        conv.UnreadCount = 0;
        conv.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true, "Typing indicator sent.");
    }

    public async Task<ApiResponse<Conversation>> AssignConversationAsync(int companyId, long conversationId, int userId, int? changedByUserId = null, bool? updateContactOwner = null, string? reason = null, CancellationToken ct = default)
    {
        var conv = await _db.Conversations
            .FirstOrDefaultAsync(c => c.CompanyId == companyId && c.ConversationId == conversationId, ct);
        if (conv is null)
            return ApiResponse<Conversation>.Fail("Conversation not found", HttpStatusCode.NotFound);

        try
        {
            var settings = await _routingService.GetOrCreateCompanySettingsAsync(companyId, ct);
            await _routingService.AssignConversationAsync(
                companyId,
                conv,
                conv.AssignedTeamId,
                userId,
                changedByUserId,
                updateContactOwner: updateContactOwner ?? settings.ManualReassignmentUpdatesContactOwner,
                assignmentMode: "MANUAL",
                reason: string.IsNullOrWhiteSpace(reason) ? "MANUAL_ASSIGN" : reason,
                notes: null,
                cancellationToken: ct);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<Conversation>.Fail(ex.Message, HttpStatusCode.BadRequest);
        }

        _logger.LogInformation("Conversation {ConvId} assigned to user {UserId}", conversationId, userId);
        return ApiResponse<Conversation>.Ok(conv);
    }

    public async Task<ApiResponse<Conversation>> AssignConversationToTeamAsync(
        int companyId,
        long conversationId,
        int teamId,
        int? userId = null,
        bool autoDistributeToTeamMember = true,
        int? changedByUserId = null,
        bool? updateContactOwner = null,
        string? reason = null,
        CancellationToken ct = default)
    {
        var conv = await _db.Conversations
            .Include(c => c.Contact)
            .FirstOrDefaultAsync(c => c.CompanyId == companyId && c.ConversationId == conversationId, ct);
        if (conv is null)
        {
            return ApiResponse<Conversation>.Fail("Conversation not found", HttpStatusCode.NotFound);
        }

        var contact = conv.Contact ?? await _db.Contacts
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.ContactId == conv.ContactId, ct);
        if (contact is null)
        {
            return ApiResponse<Conversation>.Fail("Conversation contact not found.", HttpStatusCode.BadRequest);
        }

        try
        {
            var settings = await _routingService.GetOrCreateCompanySettingsAsync(companyId, ct);
            var shouldUpdateOwner = updateContactOwner ?? settings.ManualReassignmentUpdatesContactOwner;
            var normalizedReason = string.IsNullOrWhiteSpace(reason) ? "MANUAL_TEAM_ASSIGN" : reason!;

            if (userId.HasValue)
            {
                await _routingService.AssignConversationAsync(
                    companyId,
                    conv,
                    teamId,
                    userId.Value,
                    changedByUserId,
                    updateContactOwner: shouldUpdateOwner,
                    assignmentMode: "MANUAL",
                    reason: normalizedReason,
                    notes: null,
                    cancellationToken: ct);
            }
            else if (autoDistributeToTeamMember)
            {
                var autoResult = await _routingService.AutoAssignConversationAsync(
                    companyId,
                    conv,
                    contact,
                    reason: normalizedReason,
                    preferredTeamId: teamId,
                    cancellationToken: ct);

                if (!autoResult.Changed && autoResult.NoAvailableAgent)
                {
                    await _routingService.AssignConversationAsync(
                        companyId,
                        conv,
                        teamId,
                        newAssignedUserId: null,
                        changedByUserId: changedByUserId,
                        updateContactOwner: false,
                        assignmentMode: "MANUAL",
                        reason: normalizedReason,
                        notes: "Team assigned without user because no eligible member was available.",
                        cancellationToken: ct);
                }
            }
            else
            {
                await _routingService.AssignConversationAsync(
                    companyId,
                    conv,
                    teamId,
                    newAssignedUserId: null,
                    changedByUserId: changedByUserId,
                    updateContactOwner: false,
                    assignmentMode: "MANUAL",
                    reason: normalizedReason,
                    notes: null,
                    cancellationToken: ct);
            }
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<Conversation>.Fail(ex.Message, HttpStatusCode.BadRequest);
        }

        _logger.LogInformation(
            "Conversation {ConversationId} assigned to team {TeamId} (user {UserId})",
            conversationId,
            teamId,
            userId?.ToString() ?? "-");

        return ApiResponse<Conversation>.Ok(conv);
    }

    public async Task<ApiResponse<Conversation>> UnassignConversationAsync(int companyId, long conversationId, int? changedByUserId = null, CancellationToken ct = default)
    {
        var conv = await _db.Conversations
            .FirstOrDefaultAsync(c => c.CompanyId == companyId && c.ConversationId == conversationId, ct);
        if (conv is null)
            return ApiResponse<Conversation>.Fail("Conversation not found", HttpStatusCode.NotFound);

        await _routingService.AssignConversationAsync(
            companyId,
            conv,
            null,
            null,
            changedByUserId,
            updateContactOwner: false,
            assignmentMode: "MANUAL",
            reason: "MANUAL_UNASSIGN",
            notes: null,
            cancellationToken: ct);

        _logger.LogInformation("Conversation {ConvId} unassigned", conversationId);
        return ApiResponse<Conversation>.Ok(conv);
    }

    public async Task<ApiResponse<Conversation>> PickConversationAsync(int companyId, long conversationId, int userId, bool? updateContactOwner = null, string? reason = null, CancellationToken ct = default)
    {
        var conv = await _db.Conversations
            .FirstOrDefaultAsync(c => c.CompanyId == companyId && c.ConversationId == conversationId, ct);
        if (conv is null)
            return ApiResponse<Conversation>.Fail("Conversation not found", HttpStatusCode.NotFound);

        if (conv.AssignedUserId is not null)
            return ApiResponse<Conversation>.Fail("Conversation is already assigned to another user.", HttpStatusCode.Conflict);

        try
        {
            var settings = await _routingService.GetOrCreateCompanySettingsAsync(companyId, ct);
            await _routingService.AssignConversationAsync(
                companyId,
                conv,
                conv.AssignedTeamId,
                userId,
                changedByUserId: userId,
                updateContactOwner: updateContactOwner ?? settings.ManualReassignmentUpdatesContactOwner,
                assignmentMode: "MANUAL",
                reason: string.IsNullOrWhiteSpace(reason) ? "SELF_PICK" : reason,
                notes: null,
                cancellationToken: ct);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<Conversation>.Fail(ex.Message, HttpStatusCode.BadRequest);
        }

        _logger.LogInformation("Conversation {ConvId} picked by user {UserId}", conversationId, userId);
        return ApiResponse<Conversation>.Ok(conv);
    }

    public async Task<ApiResponse<Conversation>> GetOrCreateConversationAsync(int companyId, string contactNumber, int? phoneNumberId, CancellationToken ct)
    {
        try
        {
            var resolved = await _resolver.ResolveAsync(companyId, contactNumber, phoneNumberId, source: "conversation_api", cancellationToken: ct);
            return ApiResponse<Conversation>.Ok(resolved.Conversation);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<Conversation>.Fail(ex.Message, HttpStatusCode.BadRequest);
        }
    }

    public async Task ProcessInboundMessageAsync(
        int companyId, string contactNumber, string? contactName, int whatsAppPhoneNumberId,
        string metaMessageId, string messageType, string content,
        string? mediaUrl, string? mediaMimeType, string? fileName, string? interactiveReplyId, string? interactiveReplyTitle, string? interactiveType, string? interactivePayloadJson, DateTime? occurredAtUtc, CancellationToken ct)
    {
        if (companyId <= 0 || string.IsNullOrEmpty(contactNumber))
            return;

        var eventTimestampUtc = occurredAtUtc ?? DateTime.UtcNow;
        if (eventTimestampUtc.Kind != DateTimeKind.Utc)
            eventTimestampUtc = DateTime.SpecifyKind(eventTimestampUtc, DateTimeKind.Utc);

        if (!string.IsNullOrEmpty(metaMessageId))
        {
            var exists = await _db.ConversationMessages.AnyAsync(m => m.MetaMessageId == metaMessageId, ct);
            if (exists)
                return;
        }

        ResolvedConversationContext resolved;
        try
        {
            resolved = await _resolver.ResolveAsync(
                companyId,
                contactNumber,
                whatsAppPhoneNumberId,
                contactName,
                "webhook_inbound",
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve inbound customer context for company {CompanyId}.", companyId);
            return;
        }

        var conv = resolved.Conversation;
        var contact = resolved.Contact;
        var isReaction = string.Equals(messageType, "reaction", StringComparison.OrdinalIgnoreCase);

        var msg = new ConversationMessage
        {
            ConversationId = conv.ConversationId,
            CompanyId = companyId,
            Direction = "inbound",
            MetaMessageId = metaMessageId,
            MessageType = messageType,
            Content = content,
            MediaUrl = mediaUrl,
            MediaMimeType = mediaMimeType,
            FileName = fileName,
            Status = "received",
            TimestampUtc = eventTimestampUtc
        };
        _db.ConversationMessages.Add(msg);

        conv.LastInboundMessageAtUtc = eventTimestampUtc;
        conv.UpdatedAtUtc = DateTime.UtcNow;

        contact.LastSeenAtUtc = eventTimestampUtc;
        contact.LastInboundMessageAtUtc = eventTimestampUtc;
        contact.UpdatedAtUtc = DateTime.UtcNow;

        if (!isReaction)
        {
            var preview = string.IsNullOrEmpty(content) ? $"[{messageType}]" : content;
            conv.LastMessageContent = preview.Length > 1000 ? preview[..1000] : preview;
            conv.LastMessageType = messageType;
            conv.LastMessageAtUtc = eventTimestampUtc;
            conv.UnreadCount++;
        }

        await _db.SaveChangesAsync(ct);

        if (isReaction)
        {
            _logger.LogInformation("Persisted inbound reaction {MetaId} in conversation {ConvId}", metaMessageId, conv.ConversationId);
            return;
        }

        var routingResult = await _routingService.AutoAssignConversationAsync(
            companyId,
            conv,
            contact,
            "INBOUND_MESSAGE",
            preferredTeamId: null,
            cancellationToken: ct);

        try
        {
            var contactDisplayName = string.IsNullOrWhiteSpace(conv.ContactName) ? conv.ContactNumber : conv.ContactName;
            var notificationBody = string.IsNullOrWhiteSpace(content)
                ? $"[{messageType}]"
                : (content.Length > 500 ? content[..500] : content);

            await _notificationService.CreateNotificationAsync(companyId, new CreateNotificationRequest
            {
                Type = "info",
                Title = contactDisplayName ?? conv.ContactNumber,
                Body = notificationBody,
                Category = "inbox",
                TargetUserId = conv.AssignedUserId,
                MetadataJson = JsonSerializer.Serialize(new
                {
                    conversationId = conv.ConversationId,
                    contactId = conv.ContactId,
                    contactNumber = conv.ContactNumber,
                    messageType,
                    metaMessageId,
                    noAvailableAgent = routingResult.NoAvailableAgent
                }, JsonOpts)
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create inbox notification for conversation {ConvId}", conv.ConversationId);
        }

        try
        {
            var flowResult = await _conversationFlowService.TryProcessInboundAsync(companyId, conv, contact, new FlowInboundMessage
            {
                MessageType = messageType,
                Text = content,
                SelectionId = interactiveReplyId,
                SelectionTitle = interactiveReplyTitle,
                InteractiveType = interactiveType,
                StructuredDataJson = interactivePayloadJson,
                MetaMessageId = metaMessageId
            }, ct);

            if (!flowResult.Handled)
            {
                await TryProcessAutomationAsync(companyId, conv, contact, content, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process automation/flow for conversation {ConvId}", conv.ConversationId);
        }

        _logger.LogInformation("Persisted inbound message {MetaId} in conversation {ConvId}", metaMessageId, conv.ConversationId);
    }

    public async Task ProcessStatusUpdateAsync(string metaMessageId, string status, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(metaMessageId) || string.IsNullOrEmpty(status))
            return;

        var normalizedStatus = status.Trim().ToLowerInvariant();
        var msg = await _db.ConversationMessages.FirstOrDefaultAsync(m => m.MetaMessageId == metaMessageId, ct);
        var outboundMessage = await _db.Messages.FirstOrDefaultAsync(m => m.ExternalMessageId == metaMessageId, ct);

        var order = new Dictionary<string, int>
        {
            ["sending"] = 0,
            ["sent"] = 1,
            ["delivered"] = 2,
            ["read"] = 3,
            ["failed"] = -1
        };

        if (msg is not null)
        {
            var currentOrder = order.GetValueOrDefault(msg.Status, 0);
            var newOrder = order.GetValueOrDefault(normalizedStatus, 0);

            if (newOrder > currentOrder || normalizedStatus == "failed")
            {
                msg.Status = normalizedStatus;
            }
        }

        if (outboundMessage is not null)
        {
            var nextStatus = normalizedStatus.ToUpperInvariant();
            if (!string.Equals(outboundMessage.Status, nextStatus, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedStatus, "failed", StringComparison.OrdinalIgnoreCase))
            {
                outboundMessage.Status = nextStatus;
                outboundMessage.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        if (msg is not null || outboundMessage is not null)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Updated message {MetaId} status to {Status}", metaMessageId, normalizedStatus);
        }
    }

    private async Task<ApiResponse<bool>> ValidateConversationInteractionAsync(Conversation conv, int currentUserId, string currentRole, CancellationToken ct)
    {
        var lastInboundAtUtc = conv.LastInboundMessageAtUtc;
        if (!lastInboundAtUtc.HasValue)
        {
            lastInboundAtUtc = await _db.ConversationMessages
                .Where(m => m.ConversationId == conv.ConversationId && m.Direction == "inbound")
                .OrderByDescending(m => m.TimestampUtc)
                .Select(m => (DateTime?)m.TimestampUtc)
                .FirstOrDefaultAsync(ct);
        }

        if (lastInboundAtUtc.HasValue && (DateTime.UtcNow - EnsureUtc(lastInboundAtUtc.Value)) >= TimeSpan.FromHours(24))
        {
            return ApiResponse<bool>.Fail(
                "24-hour customer support window has expired. Use an approved template message.",
                HttpStatusCode.Forbidden);
        }

        if (!string.Equals(currentRole, "Admin", StringComparison.OrdinalIgnoreCase)
            && (conv.AssignedUserId is null || conv.AssignedUserId != currentUserId))
        {
            return ApiResponse<bool>.Fail(
                "You must pick or be assigned to this conversation before sending messages.",
                HttpStatusCode.Forbidden);
        }

        return ApiResponse<bool>.Ok(true);
    }

    private async Task<TenantWhatsAppConfig> ResolveConfigAsync(int companyId, int? whatsAppPhoneNumberId, CancellationToken ct)
    {
        if (whatsAppPhoneNumberId.HasValue)
        {
            var config = await _configService.GetConfigByWhatsAppPhoneNumberIdAsync(whatsAppPhoneNumberId.Value, ct);
            if (config is not null)
                return config;
        }

        return await _configService.GetRequiredConfigAsync(companyId, ct);
    }

    private async Task TryProcessAutomationAsync(int companyId, Conversation conv, Contact contact, string incomingContent, CancellationToken ct)
    {
        var rule = await _automationService.FindMatchingRuleAsync(companyId, incomingContent ?? string.Empty, ct);
        if (rule is null)
            return;

        var payloadDefinition = BuildAutomationPayload(conv.ContactNumber, rule);
        if (payloadDefinition is null)
        {
            _logger.LogWarning(
                "Skipping automation rule {RuleId} because the response payload is incomplete.",
                rule.AutomationRuleId);
            return;
        }

        var config = await ResolveConfigAsync(companyId, conv.WhatsAppPhoneNumberId, ct);
        var payloadBody = JsonSerializer.Serialize(payloadDefinition.Value.Payload, JsonOpts);
        var now = DateTime.UtcNow;
        var duplicateThreshold = now.Subtract(AutomationDuplicateGuardWindow);

        var recentDuplicateExists = await _db.Messages.AnyAsync(m =>
            m.CompanyId == companyId
            && m.ConversationId == conv.ConversationId
            && m.Source == "AUTOMATION"
            && m.Status != "FAILED"
            && m.MessageBody == payloadBody
            && m.CreatedAtUtc >= duplicateThreshold, ct);

        if (recentDuplicateExists)
        {
            _logger.LogInformation(
                "Skipping duplicate automation reply for conversation {ConvId} within {WindowSeconds}s.",
                conv.ConversationId,
                AutomationDuplicateGuardWindow.TotalSeconds);
            return;
        }

        await _messageDispatchService.QueueLinkedMessageAsync(new QueueLinkedMessageRequest
        {
            CompanyId = companyId,
            WhatsAppPhoneNumberId = conv.WhatsAppPhoneNumberId,
            ContactId = contact.ContactId,
            ConversationId = conv.ConversationId,
            ToNumber = conv.ContactNumber,
            MessageType = payloadDefinition.Value.ResponseType.ToUpperInvariant(),
            MessageBody = payloadBody,
            Source = "AUTOMATION",
            ConversationMessageType = payloadDefinition.Value.ResponseType,
            ConversationContent = payloadDefinition.Value.Preview,
            ConversationMessageStatus = "sending",
            CreatedAtUtc = now,
            Payload = new MessageQueuePayload
            {
                Method = HttpMethod.Post.Method,
                Path = $"{config.PhoneNumberId}/messages",
                Body = payloadBody
            }
        }, ct);

        conv.LastMessageContent = payloadDefinition.Value.Preview.Length > 1000
            ? payloadDefinition.Value.Preview[..1000]
            : payloadDefinition.Value.Preview;
        conv.LastMessageType = payloadDefinition.Value.ResponseType;
        conv.LastMessageAtUtc = now;
        conv.UpdatedAtUtc = now;

        contact.LastSeenAtUtc = now;
        contact.LastOutboundMessageAtUtc = now;
        contact.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Automation rule {RuleId} queued for conversation {ConvId}.",
            rule.AutomationRuleId,
            conv.ConversationId);
    }

    private async Task<ApiResponse<GenericGraphResponse>> UploadConversationMediaAsync(
        TenantWhatsAppConfig config,
        byte[] fileData,
        string? contentType,
        string fileName,
        CancellationToken ct)
    {
        var safeFileName = SanitizeFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            return ApiResponse<GenericGraphResponse>.Fail("Invalid file name.", HttpStatusCode.BadRequest);
        }

        var normalizedContentType = string.IsNullOrWhiteSpace(contentType)
            ? "application/octet-stream"
            : contentType.Trim();

        using var formData = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileData);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(normalizedContentType);

        formData.Add(new StringContent("whatsapp"), "messaging_product");
        formData.Add(fileContent, "file", safeFileName);

        return await _graphClient.SendAsync(config, HttpMethod.Post, $"{config.PhoneNumberId}/media", formData, ct);
    }

    private static string NormalizeConversationMediaType(string? requestedType, string? contentType, string fileName)
    {
        var normalizedRequested = (requestedType ?? string.Empty).Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(normalizedRequested))
        {
            return normalizedRequested;
        }

        var normalizedContentType = (contentType ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedContentType.StartsWith("image/"))
        {
            if (normalizedContentType is "image/webp" && fileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
            {
                return "sticker";
            }

            return "image";
        }

        if (normalizedContentType.StartsWith("video/"))
        {
            return "video";
        }

        if (normalizedContentType.StartsWith("audio/"))
        {
            return "audio";
        }

        if (fileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
        {
            return "sticker";
        }

        return "document";
    }

    private static string? ExtractGraphId(GenericGraphResponse? response)
    {
        if (response is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(response.Id))
        {
            return response.Id;
        }

        if (response.AdditionalData is null)
        {
            return null;
        }

        if (response.AdditionalData.TryGetValue("id", out var idNode))
        {
            return idNode.ValueKind == JsonValueKind.String ? idNode.GetString() : idNode.ToString();
        }

        return null;
    }

    private static string SanitizeFileName(string fileName)
    {
        var candidate = Path.GetFileName(fileName)?.Trim() ?? string.Empty;
        if (candidate.Length == 0 || candidate.Length > 255)
        {
            return string.Empty;
        }

        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            candidate = candidate.Replace(invalidChar.ToString(), string.Empty, StringComparison.Ordinal);
        }

        return candidate;
    }

    private static string BuildForwardFallbackText(ConversationMessage sourceMessage)
    {
        var normalizedType = (sourceMessage.MessageType ?? string.Empty).Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(sourceMessage.Content))
        {
            return sourceMessage.Content;
        }

        return normalizedType switch
        {
            "reaction" => "[Reaction]",
            "location" => "[Location]",
            "contacts" => "[Contacts]",
            "order" => "[Order]",
            "system" => "[System]",
            _ => "[Forwarded message]"
        };
    }

    private static string BuildReactionConversationContent(string emoji, string? targetMetaMessageId)
    {
        return JsonSerializer.Serialize(new
        {
            type = "reaction",
            emoji,
            messageId = string.IsNullOrWhiteSpace(targetMetaMessageId) ? null : targetMetaMessageId.Trim()
        });
    }

    private static object BuildOutboundPayload(string toNumber, string messageType, SendConversationMessageRequest request)
    {
        object? contextObject = string.IsNullOrWhiteSpace(request.ReplyToMetaMessageId)
            ? null
            : new { message_id = request.ReplyToMetaMessageId.Trim() };

        if (messageType == "reaction")
        {
            return new Dictionary<string, object?>
            {
                ["messaging_product"] = "whatsapp",
                ["recipient_type"] = "individual",
                ["to"] = toNumber,
                ["type"] = "reaction",
                ["reaction"] = new
                {
                    message_id = request.ReplyToMetaMessageId?.Trim(),
                    emoji = request.Content ?? string.Empty
                }
            };
        }

        if (messageType == "text")
        {
            var payload = new Dictionary<string, object?>
            {
                ["messaging_product"] = "whatsapp",
                ["recipient_type"] = "individual",
                ["to"] = toNumber,
                ["type"] = "text",
                ["text"] = new { body = request.Content }
            };

            if (contextObject is not null)
            {
                payload["context"] = contextObject;
            }

            return payload;
        }

        var mediaObj = new Dictionary<string, object?>();
        if (!string.IsNullOrEmpty(request.MediaUrl))
        {
            if (request.MediaUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                mediaObj["link"] = request.MediaUrl;
            else
                mediaObj["id"] = request.MediaUrl;
        }

        if (!string.IsNullOrEmpty(request.Content)
            && (messageType == "image" || messageType == "video" || messageType == "document"))
        {
            mediaObj["caption"] = request.Content;
        }

        if (!string.IsNullOrEmpty(request.FileName) && messageType == "document")
            mediaObj["filename"] = request.FileName;

        var mediaPayload = new Dictionary<string, object?>
        {
            ["messaging_product"] = "whatsapp",
            ["recipient_type"] = "individual",
            ["to"] = toNumber,
            ["type"] = messageType,
            [messageType] = mediaObj
        };

        if (contextObject is not null)
        {
            mediaPayload["context"] = contextObject;
        }

        return mediaPayload;
    }

    private static (string ResponseType, string Preview, object Payload)? BuildAutomationPayload(string toNumber, AutomationRule rule)
    {
        var responseType = rule.ResponseType.Trim().ToLowerInvariant();

        if (responseType == "text")
        {
            if (string.IsNullOrWhiteSpace(rule.ResponseValue))
                return null;

            var preview = rule.ResponseValue.Trim();
            return (
                responseType,
                preview,
                new
                {
                    messaging_product = "whatsapp",
                    recipient_type = "individual",
                    to = toNumber,
                    type = "text",
                    text = new { body = preview }
                });
        }

        if (responseType == "template")
        {
            if (string.IsNullOrWhiteSpace(rule.TemplateName))
                return null;

            var preview = string.IsNullOrWhiteSpace(rule.ResponseValue)
                ? $"Template: {rule.TemplateName}"
                : rule.ResponseValue.Trim();

            return (
                responseType,
                preview,
                new
                {
                    messaging_product = "whatsapp",
                    recipient_type = "individual",
                    to = toNumber,
                    type = "template",
                    template = new
                    {
                        name = rule.TemplateName,
                        language = new { code = string.IsNullOrWhiteSpace(rule.LanguageCode) ? "en_US" : rule.LanguageCode }
                    }
                });
        }

        return null;
    }

    private static DateTime EnsureUtc(DateTime dateTime)
        => dateTime.Kind == DateTimeKind.Utc ? dateTime : DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
}
