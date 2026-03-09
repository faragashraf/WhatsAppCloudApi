using Microsoft.EntityFrameworkCore;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Domain.Models;
using WhatsAppCloudApi.Infrastructure.Data;

namespace WhatsAppCloudApi.Infrastructure.Services;

public sealed class MessageDispatchService : IMessageDispatchService
{
    private readonly ApplicationDbContext _dbContext;

    public MessageDispatchService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Message> QueueMessageAsync(
        int companyId,
        int? whatsAppPhoneNumberId,
        string toNumber,
        string messageType,
        string messageBody,
        MessageQueuePayload payload,
        CancellationToken cancellationToken = default)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var message = new Message
            {
                CompanyId = companyId,
                WhatsAppPhoneNumberId = whatsAppPhoneNumberId,
                ToNumber = toNumber,
                MessageType = messageType,
                MessageBody = messageBody,
                Status = "PENDING",
                CreatedAtUtc = DateTime.UtcNow
            };

            _dbContext.Messages.Add(message);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var queueItem = new MessageQueueItem
            {
                MessageId = message.MessageId,
                CompanyId = companyId,
                Status = "PENDING",
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(payload),
                RetryCount = 0,
                CreatedAtUtc = DateTime.UtcNow
            };

            _dbContext.MessageQueue.Add(queueItem);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return message;
        });
    }
}
