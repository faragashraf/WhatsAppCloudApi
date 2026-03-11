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
    private static readonly HashSet<string> AllowedMessageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text", "image", "video", "audio", "document", "sticker"
    };

    private readonly ApplicationDbContext _db;
    private readonly ILogger<ConversationService> _logger;
    private readonly ITenantWhatsAppConfigService _configService;
    private readonly IHttpClientFactory _httpClientFactory;

    public ConversationService(
        ApplicationDbContext db,
        ILogger<ConversationService> logger,
        ITenantWhatsAppConfigService configService,
        IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _logger = logger;
        _configService = configService;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ApiResponse<PagedResult<Conversation>>> GetConversationsAsync(int companyId, ConversationQueryParams query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var q = _db.Conversations
            .Include(c => c.Contact)
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
            Items = items, TotalCount = total, Page = page, PageSize = pageSize
        });
    }

    public async Task<ApiResponse<Conversation>> GetConversationByIdAsync(int companyId, long conversationId, CancellationToken ct)
    {
        var conv = await _db.Conversations
            .Include(c => c.Contact)
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
            Items = items, TotalCount = total, Page = page, PageSize = pageSize
        });
    }

    public async Task<ApiResponse<ConversationMessage>> SendMessageAsync(int companyId, long conversationId, SendConversationMessageRequest request, int currentUserId, string currentRole, CancellationToken ct)
    {
        var conv = await _db.Conversations
            .Include(c => c.WhatsAppPhoneNumber)
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

        var lastInboundAtUtc = conv.LastInboundMessageAtUtc;
        if (!lastInboundAtUtc.HasValue)
        {
            lastInboundAtUtc = await _db.ConversationMessages
                .Where(m => m.ConversationId == conversationId && m.Direction == "inbound")
                .OrderByDescending(m => m.TimestampUtc)
                .Select(m => (DateTime?)m.TimestampUtc)
                .FirstOrDefaultAsync(ct);
        }

        if (lastInboundAtUtc.HasValue && (DateTime.UtcNow - EnsureUtc(lastInboundAtUtc.Value)) >= TimeSpan.FromHours(24))
        {
            return ApiResponse<ConversationMessage>.Fail(
                "24-hour customer support window has expired. Use an approved template message.",
                HttpStatusCode.Forbidden);
        }

        // ── Enforce pick/assign: non-admin users must be assigned to this conversation ──
        if (!string.Equals(currentRole, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            if (conv.AssignedUserId is null || conv.AssignedUserId != currentUserId)
                return ApiResponse<ConversationMessage>.Fail("You must pick or be assigned to this conversation before sending messages.", HttpStatusCode.Forbidden);
        }

        var msg = new ConversationMessage
        {
            ConversationId = conversationId,
            CompanyId = companyId,
            Direction = "outbound",
            MessageType = messageType,
            Content = request.Content,
            MediaUrl = request.MediaUrl,
            MediaMimeType = request.MediaMimeType,
            Status = "sending",
        };
        _db.ConversationMessages.Add(msg);

        conv.LastMessageContent = request.Content.Length > 1000 ? request.Content[..1000] : request.Content;
        conv.LastMessageType = messageType;
        conv.LastMessageAtUtc = DateTime.UtcNow;
        conv.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // ── Send via WhatsApp Cloud API ──
        try
        {
            var phoneNumberId = conv.WhatsAppPhoneNumber?.PhoneNumberId;
            TenantWhatsAppConfig config;
            if (!string.IsNullOrEmpty(phoneNumberId))
                config = await _configService.GetConfigByPhoneNumberIdAsync(phoneNumberId, ct)
                         ?? await _configService.GetRequiredConfigAsync(companyId, ct);
            else
                config = await _configService.GetRequiredConfigAsync(companyId, ct);

            object payload;
            if (messageType == "text")
            {
                payload = new
                {
                    messaging_product = "whatsapp",
                    recipient_type = "individual",
                    to = conv.ContactNumber,
                    type = "text",
                    text = new { body = request.Content }
                };
            }
            else
            {
                var mediaObj = new Dictionary<string, object?>();
                if (!string.IsNullOrEmpty(request.MediaUrl))
                {
                    if (request.MediaUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        mediaObj["link"] = request.MediaUrl;
                    else
                        mediaObj["id"] = request.MediaUrl;
                }
                if (!string.IsNullOrEmpty(request.Content))
                    mediaObj["caption"] = request.Content;
                if (!string.IsNullOrEmpty(request.FileName))
                    mediaObj["filename"] = request.FileName;

                payload = new Dictionary<string, object?>
                {
                    ["messaging_product"] = "whatsapp",
                    ["recipient_type"] = "individual",
                    ["to"] = conv.ContactNumber,
                    ["type"] = messageType,
                    [messageType] = mediaObj,
                };
            }

            var json = JsonSerializer.Serialize(payload, JsonOpts);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post,
                $"https://graph.facebook.com/v21.0/{config.PhoneNumberId}/messages")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.AccessToken);

            using var client = _httpClientFactory.CreateClient();
            var response = await client.SendAsync(httpRequest, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("messages", out var msgs) && msgs.GetArrayLength() > 0)
                    msg.MetaMessageId = msgs[0].GetProperty("id").GetString();
                msg.Status = "sent";
            }
            else
            {
                msg.Status = "failed";
                msg.FailureReason = responseBody.Length > 500 ? responseBody[..500] : responseBody;
                _logger.LogWarning("WhatsApp send failed: {Response}", responseBody);
            }
        }
        catch (Exception ex)
        {
            msg.Status = "failed";
            msg.FailureReason = ex.Message;
            _logger.LogError(ex, "Failed to send WhatsApp message for conversation {Id}", conversationId);
        }

        await _db.SaveChangesAsync(ct);
        return ApiResponse<ConversationMessage>.Ok(msg);
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

    public async Task<ApiResponse<Conversation>> AssignConversationAsync(int companyId, long conversationId, int userId, CancellationToken ct)
    {
        var conv = await _db.Conversations.FirstOrDefaultAsync(c => c.CompanyId == companyId && c.ConversationId == conversationId, ct);
        if (conv is null)
            return ApiResponse<Conversation>.Fail("Conversation not found", HttpStatusCode.NotFound);

        var user = await _db.CompanyUsers.FirstOrDefaultAsync(u => u.CompanyUserId == userId && u.CompanyId == companyId && u.IsActive, ct);
        if (user is null)
            return ApiResponse<Conversation>.Fail("User not found or inactive", HttpStatusCode.BadRequest);

        conv.AssignedUserId = userId;
        conv.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Conversation {ConvId} assigned to user {UserId}", conversationId, userId);
        return ApiResponse<Conversation>.Ok(conv);
    }

    public async Task<ApiResponse<Conversation>> UnassignConversationAsync(int companyId, long conversationId, CancellationToken ct)
    {
        var conv = await _db.Conversations.FirstOrDefaultAsync(c => c.CompanyId == companyId && c.ConversationId == conversationId, ct);
        if (conv is null)
            return ApiResponse<Conversation>.Fail("Conversation not found", HttpStatusCode.NotFound);

        conv.AssignedUserId = null;
        conv.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Conversation {ConvId} unassigned", conversationId);
        return ApiResponse<Conversation>.Ok(conv);
    }

    public async Task<ApiResponse<Conversation>> PickConversationAsync(int companyId, long conversationId, int userId, CancellationToken ct)
    {
        var conv = await _db.Conversations.FirstOrDefaultAsync(c => c.CompanyId == companyId && c.ConversationId == conversationId, ct);
        if (conv is null)
            return ApiResponse<Conversation>.Fail("Conversation not found", HttpStatusCode.NotFound);

        if (conv.AssignedUserId is not null)
            return ApiResponse<Conversation>.Fail("Conversation is already assigned to another user.", HttpStatusCode.Conflict);

        var user = await _db.CompanyUsers.FirstOrDefaultAsync(u => u.CompanyUserId == userId && u.CompanyId == companyId && u.IsActive, ct);
        if (user is null)
            return ApiResponse<Conversation>.Fail("User not found or inactive", HttpStatusCode.BadRequest);

        conv.AssignedUserId = userId;
        conv.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Conversation {ConvId} picked by user {UserId}", conversationId, userId);
        return ApiResponse<Conversation>.Ok(conv);
    }

    public async Task<ApiResponse<Conversation>> GetOrCreateConversationAsync(int companyId, string contactNumber, int? phoneNumberId, CancellationToken ct)
    {
        var conv = await _db.Conversations
            .FirstOrDefaultAsync(c => c.CompanyId == companyId && c.ContactNumber == contactNumber && c.WhatsAppPhoneNumberId == phoneNumberId, ct);

        if (conv is not null)
            return ApiResponse<Conversation>.Ok(conv);

        var contact = await _db.Contacts.FirstOrDefaultAsync(c => c.CompanyId == companyId && c.PhoneNumber == contactNumber, ct);

        conv = new Conversation
        {
            CompanyId = companyId,
            ContactNumber = contactNumber,
            ContactName = contact?.Name,
            ContactId = contact?.ContactId,
            WhatsAppPhoneNumberId = phoneNumberId,
            Status = "OPEN",
        };

        _db.Conversations.Add(conv);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<Conversation>.Ok(conv);
    }

    // ─── Webhook processing ─────────────────────────────────────

    public async Task ProcessInboundMessageAsync(
        int companyId, string contactNumber, string? contactName, int whatsAppPhoneNumberId,
        string metaMessageId, string messageType, string content,
        string? mediaUrl, string? mediaMimeType, DateTime? occurredAtUtc, CancellationToken ct)
    {
        if (companyId <= 0 || string.IsNullOrEmpty(contactNumber)) return;
        var eventTimestampUtc = (occurredAtUtc ?? DateTime.UtcNow);
        if (eventTimestampUtc.Kind != DateTimeKind.Utc)
        {
            eventTimestampUtc = DateTime.SpecifyKind(eventTimestampUtc, DateTimeKind.Utc);
        }

        // Dedup by MetaMessageId
        if (!string.IsNullOrEmpty(metaMessageId))
        {
            var exists = await _db.ConversationMessages.AnyAsync(m => m.MetaMessageId == metaMessageId, ct);
            if (exists) return;
        }

        var convResult = await GetOrCreateConversationAsync(companyId, contactNumber, whatsAppPhoneNumberId, ct);
        if (!convResult.Success || convResult.Data is null) return;
        var conv = convResult.Data;

        if (!string.IsNullOrEmpty(contactName) && string.IsNullOrEmpty(conv.ContactName))
        {
            conv.ContactName = contactName;
        }

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

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Persisted inbound message {MetaId} in conversation {ConvId}", metaMessageId, conv.ConversationId);
    }

    public async Task ProcessStatusUpdateAsync(string metaMessageId, string status, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(metaMessageId) || string.IsNullOrEmpty(status)) return;

        var msg = await _db.ConversationMessages
            .FirstOrDefaultAsync(m => m.MetaMessageId == metaMessageId, ct);
        if (msg is null) return;

        var order = new Dictionary<string, int>
        {
            ["sending"] = 0, ["sent"] = 1, ["delivered"] = 2, ["read"] = 3, ["failed"] = -1
        };

        var currentOrder = order.GetValueOrDefault(msg.Status, 0);
        var newOrder = order.GetValueOrDefault(status, 0);

        if (newOrder > currentOrder || status == "failed")
        {
            msg.Status = status;
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Updated message {MetaId} status to {Status}", metaMessageId, status);
        }
    }

    private static DateTime EnsureUtc(DateTime dateTime)
        => dateTime.Kind == DateTimeKind.Utc ? dateTime : DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
}
