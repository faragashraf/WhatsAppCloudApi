using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Domain.Models;
using WhatsAppCloudApi.Infrastructure.Data;

namespace WhatsAppCloudApi.Infrastructure.Services;

public sealed class MessageQueueProcessor : IMessageQueueProcessor
{
    private const int MaxRetries = 3;
    private static readonly TimeSpan ProcessingStaleAfter = TimeSpan.FromMinutes(5);

    private readonly ApplicationDbContext _dbContext;
    private readonly ITenantWhatsAppConfigService _tenantConfigService;
    private readonly IWhatsAppGraphClient _graphClient;
    private readonly ILogger<MessageQueueProcessor> _logger;

    public MessageQueueProcessor(
        ApplicationDbContext dbContext,
        ITenantWhatsAppConfigService tenantConfigService,
        IWhatsAppGraphClient graphClient,
        ILogger<MessageQueueProcessor> logger)
    {
        _dbContext = dbContext;
        _tenantConfigService = tenantConfigService;
        _graphClient = graphClient;
        _logger = logger;
    }

    public async Task<int> ProcessPendingAsync(int take, CancellationToken cancellationToken = default)
    {
        var safeTake = Math.Clamp(take, 1, 200);
        var staleBefore = DateTime.UtcNow.Subtract(ProcessingStaleAfter);
        var queueIds = await _dbContext.MessageQueue
            .AsNoTracking()
            .Where(x =>
                x.RetryCount < MaxRetries &&
                (
                    x.Status == "PENDING"
                    || (x.Status == "PROCESSING" && x.LastAttemptAtUtc.HasValue && x.LastAttemptAtUtc.Value < staleBefore)
                ))
            .OrderBy(x => x.CreatedAtUtc)
            .Take(safeTake)
            .Select(x => x.MessageQueueId)
            .ToListAsync(cancellationToken);

        var processed = 0;
        foreach (var queueId in queueIds)
        {
            if (await ProcessOneAsync(queueId, cancellationToken))
            {
                processed++;
            }
        }

        return processed;
    }

    private async Task<bool> ProcessOneAsync(long queueId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var staleBefore = now.Subtract(ProcessingStaleAfter);
        var claimed = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE [MessageQueue]
               SET [Status] = {"PROCESSING"},
                   [LastAttemptAtUtc] = {now},
                   [UpdatedAtUtc] = {now}
               WHERE [MessageQueueId] = {queueId}
                 AND [RetryCount] < {MaxRetries}
                 AND (
                     [Status] = {"PENDING"}
                     OR ([Status] = {"PROCESSING"} AND [LastAttemptAtUtc] IS NOT NULL AND [LastAttemptAtUtc] < {staleBefore})
                 );",
            cancellationToken);

        if (claimed != 1)
        {
            return false;
        }

        var queueItem = await _dbContext.MessageQueue
            .FirstOrDefaultAsync(x => x.MessageQueueId == queueId, cancellationToken);
        if (queueItem is null)
        {
            return false;
        }

        var message = await _dbContext.Messages
            .FirstOrDefaultAsync(x => x.MessageId == queueItem.MessageId, cancellationToken);

        if (message is null)
        {
            queueItem.Status = "FAILED";
            queueItem.LastError = "Message record was not found.";
            queueItem.UpdatedAtUtc = DateTime.UtcNow;
            queueItem.LastAttemptAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        try
        {
            var tenantConfig = message.WhatsAppPhoneNumberId.HasValue
                ? await _tenantConfigService.GetConfigByWhatsAppPhoneNumberIdAsync(message.WhatsAppPhoneNumberId.Value, cancellationToken)
                    ?? await _tenantConfigService.GetRequiredConfigAsync(queueItem.CompanyId, cancellationToken)
                : await _tenantConfigService.GetRequiredConfigAsync(queueItem.CompanyId, cancellationToken);
            var payload = JsonSerializer.Deserialize<MessageQueuePayload>(queueItem.PayloadJson) ?? new MessageQueuePayload();
            var method = new HttpMethod(payload.Method);
            var linkedConversationMessage = await _dbContext.ConversationMessages
                .FirstOrDefaultAsync(x => x.MessageId == message.MessageId, cancellationToken);

            HttpContent? content = null;
            if (!string.IsNullOrWhiteSpace(payload.Body) && method != HttpMethod.Get && method != HttpMethod.Head)
            {
                content = new StringContent(payload.Body, Encoding.UTF8, "application/json");
            }

            var result = await _graphClient.SendAsync(tenantConfig, method, payload.Path, content, cancellationToken);
            queueItem.LastAttemptAtUtc = DateTime.UtcNow;
            queueItem.UpdatedAtUtc = DateTime.UtcNow;

            if (result.Success)
            {
                var externalMessageId = ExtractExternalMessageId(result.Data);

                message.Status = "SENT";
                message.UpdatedAtUtc = DateTime.UtcNow;
                message.ExternalMessageId = externalMessageId;

                queueItem.Status = "SENT";
                queueItem.LastError = null;

                if (linkedConversationMessage is not null)
                {
                    linkedConversationMessage.Status = "sent";
                    linkedConversationMessage.MetaMessageId ??= externalMessageId;
                }
            }
            else
            {
                HandleFailure(queueItem, message, linkedConversationMessage, result.Error?.Details ?? result.Message ?? "Unknown Graph API error.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Queue processing failed for MessageQueueId={MessageQueueId}", queueItem.MessageQueueId);
            var linkedConversationMessage = await _dbContext.ConversationMessages
                .FirstOrDefaultAsync(x => x.MessageId == message.MessageId, cancellationToken);
            HandleFailure(queueItem, message, linkedConversationMessage, ex.Message);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string? ExtractExternalMessageId(GenericGraphResponse? response)
    {
        if (response is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(response.Id))
        {
            return response.Id;
        }

        if (response.Messages is null)
        {
            return null;
        }

        foreach (var item in response.Messages)
        {
            if (item is JsonElement element
                && element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty("id", out var idNode))
            {
                var id = idNode.GetString();
                if (!string.IsNullOrWhiteSpace(id))
                {
                    return id;
                }
            }
        }

        return null;
    }

    private static void HandleFailure(MessageQueueItem queueItem, Message message, ConversationMessage? linkedConversationMessage, string failureReason)
    {
        queueItem.RetryCount += 1;
        queueItem.LastError = failureReason;
        queueItem.Status = queueItem.RetryCount >= MaxRetries ? "FAILED" : "PENDING";
        queueItem.UpdatedAtUtc = DateTime.UtcNow;
        queueItem.LastAttemptAtUtc = DateTime.UtcNow;

        message.Status = queueItem.RetryCount >= MaxRetries ? "FAILED" : "PENDING";
        message.FailureReason = failureReason;
        message.UpdatedAtUtc = DateTime.UtcNow;

        if (linkedConversationMessage is not null && queueItem.RetryCount >= MaxRetries)
        {
            linkedConversationMessage.Status = "failed";
            linkedConversationMessage.FailureReason = failureReason;
        }
    }
}
