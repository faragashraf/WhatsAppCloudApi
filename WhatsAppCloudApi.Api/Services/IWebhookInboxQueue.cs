using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Infrastructure.Data;

namespace WhatsAppCloudApi.Api.Services;

public interface IWebhookInboxQueue
{
    Task<long> EnqueueAsync(WebhookInboxQueueItem item, CancellationToken cancellationToken = default);
}

public sealed class WebhookInboxQueueItem
{
    public int CompanyId { get; init; }
    public int WhatsAppPhoneNumberId { get; init; }
    public string PhoneNumberId { get; init; } = string.Empty;
    public string PayloadJson { get; init; } = string.Empty;
}

public sealed class DatabaseWebhookInboxQueue : IWebhookInboxQueue
{
    private readonly ApplicationDbContext _dbContext;

    public DatabaseWebhookInboxQueue(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<long> EnqueueAsync(WebhookInboxQueueItem item, CancellationToken cancellationToken = default)
    {
        var entity = new WebhookInboxItem
        {
            CompanyId = item.CompanyId,
            WhatsAppPhoneNumberId = item.WhatsAppPhoneNumberId,
            PhoneNumberId = item.PhoneNumberId,
            PayloadJson = item.PayloadJson,
            Status = "PENDING",
            RetryCount = 0,
            ReceivedAtUtc = DateTime.UtcNow
        };

        _dbContext.WebhookInbox.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity.WebhookInboxId;
    }
}
