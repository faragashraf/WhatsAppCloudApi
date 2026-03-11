namespace WhatsAppCloudApi.Application.Interfaces;

public interface IEmailQueueProcessor
{
    Task<int> ProcessPendingAsync(int take, CancellationToken cancellationToken = default);
}
