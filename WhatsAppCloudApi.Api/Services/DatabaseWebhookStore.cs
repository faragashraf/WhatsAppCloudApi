using Microsoft.EntityFrameworkCore;
using WhatsAppCloudApi.Infrastructure.Data;
using WhatsAppCloudApi.Domain.Entities;

namespace WhatsAppCloudApi.Api.Services;

public sealed class DatabaseWebhookStore : IWebhookStore
{
    private const int MaxStoredPayloadLength = 50_000;
    private readonly ApplicationDbContext _dbContext;

    public DatabaseWebhookStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(WebhookLogEntry entry, CancellationToken cancellationToken = default)
    {
        var payload = entry.Payload ?? string.Empty;
        if (payload.Length > MaxStoredPayloadLength)
        {
            payload = payload[..MaxStoredPayloadLength];
        }

        var entity = new WebhookLog
        {
            CompanyId = entry.CompanyId,
            PhoneNumberId = string.IsNullOrWhiteSpace(entry.PhoneNumberId) ? "unknown" : entry.PhoneNumberId,
            Payload = payload,
            Summary = string.IsNullOrWhiteSpace(entry.Summary) ? null : entry.Summary.Trim(),
            SignatureValid = entry.SignatureValid,
            CorrelationId = string.IsNullOrWhiteSpace(entry.CorrelationId) ? null : entry.CorrelationId.Trim(),
            CreatedAtUtc = entry.Timestamp == default ? DateTime.UtcNow : entry.Timestamp.UtcDateTime
        };

        _dbContext.WebhookLogs.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WebhookLogEntry>> GetAllAsync(int companyId, int take = 500, CancellationToken cancellationToken = default)
    {
        var safeTake = Math.Clamp(take, 1, 1000);
        var rows = await _dbContext.WebhookLogs
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(safeTake)
            .ToListAsync(cancellationToken);

        return rows.Select(x => new WebhookLogEntry
        {
            Id = x.WebhookLogId.ToString(),
            CompanyId = x.CompanyId,
            PhoneNumberId = x.PhoneNumberId,
            Timestamp = x.CreatedAtUtc,
            Payload = x.Payload,
            Summary = x.Summary,
            SignatureValid = x.SignatureValid,
            CorrelationId = x.CorrelationId
        }).ToArray();
    }
}
