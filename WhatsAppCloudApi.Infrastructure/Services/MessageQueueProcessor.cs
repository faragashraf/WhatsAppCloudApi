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
        var queueItems = await _dbContext.MessageQueue
            .Where(x => x.Status == "PENDING" && x.RetryCount < 3)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

        var processed = 0;
        foreach (var item in queueItems)
        {
            processed++;
            await ProcessOneAsync(item, cancellationToken);
        }

        return processed;
    }

    private async Task ProcessOneAsync(MessageQueueItem queueItem, CancellationToken cancellationToken)
    {
        var message = await _dbContext.Messages
            .FirstOrDefaultAsync(x => x.MessageId == queueItem.MessageId, cancellationToken);

        if (message is null)
        {
            queueItem.Status = "FAILED";
            queueItem.LastError = "Message record was not found.";
            queueItem.UpdatedAtUtc = DateTime.UtcNow;
            queueItem.LastAttemptAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
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
                message.Status = "SENT";
                message.UpdatedAtUtc = DateTime.UtcNow;
                message.ExternalMessageId = result.Data?.Id;

                queueItem.Status = "SENT";
                queueItem.LastError = null;

                if (linkedConversationMessage is not null)
                {
                    linkedConversationMessage.Status = "sent";
                    linkedConversationMessage.MetaMessageId ??= result.Data?.Id;
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
    }

    private static void HandleFailure(MessageQueueItem queueItem, Message message, ConversationMessage? linkedConversationMessage, string failureReason)
    {
        queueItem.RetryCount += 1;
        queueItem.LastError = failureReason;
        queueItem.Status = queueItem.RetryCount >= 3 ? "FAILED" : "PENDING";
        queueItem.UpdatedAtUtc = DateTime.UtcNow;
        queueItem.LastAttemptAtUtc = DateTime.UtcNow;

        message.Status = queueItem.RetryCount >= 3 ? "FAILED" : "PENDING";
        message.FailureReason = failureReason;
        message.UpdatedAtUtc = DateTime.UtcNow;

        if (linkedConversationMessage is not null && queueItem.RetryCount >= 3)
        {
            linkedConversationMessage.Status = "failed";
            linkedConversationMessage.FailureReason = failureReason;
        }
    }
}
