using System.Collections.Concurrent;
using WhatsAppCloudApi.Api.Services;

namespace WhatsAppCloudApi.Api.Services;

public sealed class InMemoryWebhookStore : IWebhookStore
{
    private readonly ConcurrentQueue<WebhookLogEntry> _queue = new();

    public void Add(WebhookLogEntry entry)
    {
        _queue.Enqueue(entry);
        while (_queue.Count > 1000)
        {
            _queue.TryDequeue(out _);
        }
    }

    public IReadOnlyList<WebhookLogEntry> GetAll()
        => _queue.ToArray();
}
