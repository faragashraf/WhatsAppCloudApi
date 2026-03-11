using WhatsAppCloudApi.Application.Interfaces;

namespace WhatsAppCloudApi.Api.BackgroundWorkers;

public sealed class EmailQueueWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailQueueWorker> _logger;

    public EmailQueueWorker(IServiceScopeFactory scopeFactory, ILogger<EmailQueueWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Email queue worker started.");
        var consecutiveFailures = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IEmailQueueProcessor>();
                var processed = await processor.ProcessPendingAsync(20, stoppingToken);
                consecutiveFailures = 0;

                if (processed == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
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
                _logger.LogError(ex, "Email queue worker cycle failed (attempt {Attempt}). Retrying in {Delay}s.", consecutiveFailures, delaySeconds);
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

        _logger.LogInformation("Email queue worker stopped.");
    }
}
