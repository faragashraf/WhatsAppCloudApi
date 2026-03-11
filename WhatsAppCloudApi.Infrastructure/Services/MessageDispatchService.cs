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

    public async Task<QueuedLinkedMessageResult> QueueLinkedMessageAsync(
        QueueLinkedMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var message = new Message
            {
                CompanyId = request.CompanyId,
                WhatsAppPhoneNumberId = request.WhatsAppPhoneNumberId,
                ContactId = request.ContactId,
                ConversationId = request.ConversationId,
                CreatedByUserId = request.CreatedByUserId,
                ToNumber = request.ToNumber,
                MessageType = request.MessageType,
                MessageBody = request.MessageBody,
                Status = "PENDING",
                Source = request.Source,
                CreatedAtUtc = request.CreatedAtUtc
            };

            _dbContext.Messages.Add(message);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var conversationMessage = new ConversationMessage
            {
                ConversationId = request.ConversationId ?? 0,
                CompanyId = request.CompanyId,
                MessageId = message.MessageId,
                Direction = "outbound",
                MessageType = request.ConversationMessageType,
                Content = request.ConversationContent,
                MediaUrl = request.MediaUrl,
                MediaMimeType = request.MediaMimeType,
                FileName = request.FileName,
                Status = request.ConversationMessageStatus,
                TimestampUtc = request.CreatedAtUtc
            };

            _dbContext.ConversationMessages.Add(conversationMessage);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var queueItem = new MessageQueueItem
            {
                MessageId = message.MessageId,
                CompanyId = request.CompanyId,
                Status = "PENDING",
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(request.Payload),
                RetryCount = 0,
                CreatedAtUtc = request.CreatedAtUtc
            };

            _dbContext.MessageQueue.Add(queueItem);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return new QueuedLinkedMessageResult
            {
                Message = message,
                ConversationMessage = conversationMessage
            };
        });
    }
}
