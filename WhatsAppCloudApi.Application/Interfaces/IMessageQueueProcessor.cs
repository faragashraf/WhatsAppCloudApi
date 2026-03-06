namespace WhatsAppCloudApi.Application.Interfaces;

public interface IMessageQueueProcessor
{
    Task<int> ProcessPendingAsync(int take, CancellationToken cancellationToken = default);
}
