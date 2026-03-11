using System.Collections.Concurrent;
using WhatsAppCloudApi.Api.Services;

namespace WhatsAppCloudApi.Api.Services;

public sealed class InMemoryWebhookStore : IWebhookStore
{
    private const int MaxEntries = 1000;
    private readonly ConcurrentQueue<WebhookLogEntry> _queue = new();

    public Task AddAsync(WebhookLogEntry entry, CancellationToken cancellationToken = default)
    {
        _queue.Enqueue(entry);
        while (_queue.Count > MaxEntries)
        {
            _queue.TryDequeue(out _);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WebhookLogEntry>> GetAllAsync(int companyId, int take = 500, CancellationToken cancellationToken = default)
    {
        var safeTake = Math.Clamp(take, 1, MaxEntries);
        var result = _queue
            .Where(x => x.CompanyId == companyId)
            .OrderByDescending(x => x.Timestamp)
            .Take(safeTake)
            .ToArray();

        return Task.FromResult<IReadOnlyList<WebhookLogEntry>>(result);
    }
}
