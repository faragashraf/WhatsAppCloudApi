using WhatsAppCloudApi.Api.Services;

namespace WhatsAppCloudApi.Api.BackgroundWorkers;

public sealed class WebhookQueueWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WebhookQueueWorker> _logger;

    public WebhookQueueWorker(IServiceScopeFactory scopeFactory, ILogger<WebhookQueueWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Webhook queue worker started.");
        var consecutiveFailures = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IWebhookInboxProcessor>();
                var processed = await processor.ProcessPendingAsync(50, stoppingToken);
                consecutiveFailures = 0;

                if (processed == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                consecutiveFailures++;
                var delaySeconds = Math.Min(5 * Math.Pow(2, consecutiveFailures - 1), 60);
                _logger.LogError(
                    ex,
                    "Webhook queue worker cycle failed (attempt {Attempt}). Retrying in {DelaySeconds}s.",
                    consecutiveFailures,
                    delaySeconds);

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("Webhook queue worker stopped.");
    }
}
