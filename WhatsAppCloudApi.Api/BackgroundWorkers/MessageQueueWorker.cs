using WhatsAppCloudApi.Application.Interfaces;

namespace WhatsAppCloudApi.Api.BackgroundWorkers;

public sealed class MessageQueueWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MessageQueueWorker> _logger;

    public MessageQueueWorker(IServiceScopeFactory scopeFactory, ILogger<MessageQueueWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Message queue worker started.");
        int consecutiveFailures = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IMessageQueueProcessor>();
                var processed = await processor.ProcessPendingAsync(20, stoppingToken);
                consecutiveFailures = 0;
                if (processed == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                consecutiveFailures++;
                var delaySeconds = Math.Min(5 * Math.Pow(2, consecutiveFailures - 1), 120);
                _logger.LogError(ex, "Message queue worker cycle failed (attempt {Attempt}). Retrying in {Delay}s.", consecutiveFailures, delaySeconds);
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

        _logger.LogInformation("Message queue worker stopped.");
    }
}
