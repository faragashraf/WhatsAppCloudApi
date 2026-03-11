using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Domain.Models;

namespace WhatsAppCloudApi.Application.Interfaces;

public interface IMessageDispatchService
{
    Task<Message> QueueMessageAsync(
        int companyId,
        int? whatsAppPhoneNumberId,
        string toNumber,
        string messageType,
        string messageBody,
        MessageQueuePayload payload,
        CancellationToken cancellationToken = default);

    Task<QueuedLinkedMessageResult> QueueLinkedMessageAsync(
        QueueLinkedMessageRequest request,
        CancellationToken cancellationToken = default);
}
