using Microsoft.EntityFrameworkCore;
using WhatsAppCloudApi.Infrastructure.Data;

namespace WhatsAppCloudApi.Api.Services;

public interface IWebhookInboxProcessor
{
    Task<int> ProcessPendingAsync(int take, CancellationToken cancellationToken = default);
}

public sealed class WebhookInboxProcessor : IWebhookInboxProcessor
{
    private const int MaxRetries = 5;
    private static readonly TimeSpan ProcessingStaleAfter = TimeSpan.FromMinutes(5);

    private readonly ApplicationDbContext _dbContext;
    private readonly IWebhookPayloadProcessor _payloadProcessor;
    private readonly ILogger<WebhookInboxProcessor> _logger;

    public WebhookInboxProcessor(
        ApplicationDbContext dbContext,
        IWebhookPayloadProcessor payloadProcessor,
        ILogger<WebhookInboxProcessor> logger)
    {
        _dbContext = dbContext;
        _payloadProcessor = payloadProcessor;
        _logger = logger;
    }

    public async Task<int> ProcessPendingAsync(int take, CancellationToken cancellationToken = default)
    {
        var safeTake = Math.Clamp(take, 1, 200);
        var staleBefore = DateTime.UtcNow.Subtract(ProcessingStaleAfter);
        var queueIds = await _dbContext.WebhookInbox
            .AsNoTracking()
            .Where(x =>
                x.RetryCount < MaxRetries &&
                (
                    x.Status == "PENDING"
                    || (x.Status == "PROCESSING" && x.LastAttemptAtUtc.HasValue && x.LastAttemptAtUtc.Value < staleBefore)
                ))
            .OrderBy(x => x.ReceivedAtUtc)
            .Take(safeTake)
            .Select(x => x.WebhookInboxId)
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
            $@"UPDATE [WebhookInbox]
               SET [Status] = {"PROCESSING"},
                   [LastAttemptAtUtc] = {now},
                   [UpdatedAtUtc] = {now}
               WHERE [WebhookInboxId] = {queueId}
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

        var row = await _dbContext.WebhookInbox.FirstOrDefaultAsync(x => x.WebhookInboxId == queueId, cancellationToken);
        if (row is null)
        {
            return false;
        }

        try
        {
            await _payloadProcessor.ProcessAsync(row.PayloadJson, row.CompanyId, row.WhatsAppPhoneNumberId, cancellationToken);
            row.Status = "PROCESSED";
            row.ProcessedAtUtc = DateTime.UtcNow;
            row.LastError = null;
            row.UpdatedAtUtc = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            row.RetryCount += 1;
            row.LastError = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
            row.LastAttemptAtUtc = DateTime.UtcNow;
            row.UpdatedAtUtc = DateTime.UtcNow;
            row.Status = row.RetryCount >= MaxRetries ? "FAILED" : "PENDING";
            _logger.LogError(ex, "Webhook queue processing failed for item {WebhookInboxId}.", row.WebhookInboxId);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
