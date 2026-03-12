using System.Net;
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
        "text", "image", "video", "audio", "document", "sticker"
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

        if (messageType != "text" && string.IsNullOrWhiteSpace(request.MediaUrl))
            return ApiResponse<ConversationMessage>.Fail("Media URL or media ID is required.", HttpStatusCode.BadRequest);

        request.Content = (request.Content ?? string.Empty).Trim();

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
            ConversationContent = request.Content,
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

        var preview = string.IsNullOrWhiteSpace(request.Content) ? $"[{messageType}]" : request.Content;
        conv.LastMessageContent = preview.Length > 1000 ? preview[..1000] : preview;
        conv.LastMessageType = messageType;
        conv.LastMessageAtUtc = now;
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
        string? mediaUrl, string? mediaMimeType, string? fileName, string? interactiveReplyId, string? interactiveReplyTitle, DateTime? occurredAtUtc, CancellationToken ct)
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

        var preview = string.IsNullOrEmpty(content) ? $"[{messageType}]" : content;
        conv.LastMessageContent = preview.Length > 1000 ? preview[..1000] : preview;
        conv.LastMessageType = messageType;
        conv.LastMessageAtUtc = eventTimestampUtc;
        conv.LastInboundMessageAtUtc = eventTimestampUtc;
        conv.UnreadCount++;
        conv.UpdatedAtUtc = DateTime.UtcNow;

        contact.LastSeenAtUtc = eventTimestampUtc;
        contact.LastInboundMessageAtUtc = eventTimestampUtc;
        contact.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        var routingResult = await _routingService.AutoAssignConversationAsync(
            companyId,
            conv,
            contact,
            "INBOUND_MESSAGE",
            ct);

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

    private static object BuildOutboundPayload(string toNumber, string messageType, SendConversationMessageRequest request)
    {
        if (messageType == "text")
        {
            return new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = toNumber,
                type = "text",
                text = new { body = request.Content }
            };
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

        return new Dictionary<string, object?>
        {
            ["messaging_product"] = "whatsapp",
            ["recipient_type"] = "individual",
            ["to"] = toNumber,
            ["type"] = messageType,
            [messageType] = mediaObj
        };
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
