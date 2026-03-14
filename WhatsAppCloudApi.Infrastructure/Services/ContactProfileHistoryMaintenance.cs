using Microsoft.EntityFrameworkCore;
using WhatsAppCloudApi.Domain.Configuration;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Infrastructure.Data;

namespace WhatsAppCloudApi.Infrastructure.Services;

internal static class ContactProfileHistoryMaintenance
{
    public static IReadOnlyList<ContactProfileHistory> CompactPendingRows(
        IEnumerable<ContactProfileHistory> rows,
        ContactProfileHistoryOptions options)
    {
        var orderedRows = rows
            .Where(x => x is not null)
            .OrderBy(x => x.CreatedAtUtc)
            .ToList();

        if (orderedRows.Count <= 1)
        {
            return orderedRows;
        }

        if (!options.EnablePendingCompaction)
        {
            return orderedRows;
        }

        var compacted = orderedRows
            .GroupBy(BuildBatchDeduplicationKey, StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(x => x.CreatedAtUtc)
                .ThenByDescending(x => x.ContactProfileHistoryId)
                .First())
            .OrderBy(x => x.CreatedAtUtc)
            .ToList();

        return compacted;
    }

    public static async Task EnforcePerContactLimitAsync(
        ApplicationDbContext db,
        int companyId,
        long contactId,
        ContactProfileHistoryOptions options,
        CancellationToken cancellationToken)
    {
        var maxRowsPerContact = options.MaxRowsPerContact;
        var targetRowsPerContact = options.TargetRowsPerContact;

        var persistedCount = await db.ContactProfileHistory
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.ContactId == contactId)
            .CountAsync(cancellationToken);

        var pendingAddedCount = db.ChangeTracker
            .Entries<ContactProfileHistory>()
            .Count(entry =>
                entry.State == EntityState.Added
                && entry.Entity.CompanyId == companyId
                && entry.Entity.ContactId == contactId);

        var totalCount = persistedCount + pendingAddedCount;
        if (totalCount <= maxRowsPerContact)
        {
            return;
        }

        var rowsToDelete = totalCount - targetRowsPerContact;
        if (rowsToDelete <= 0)
        {
            return;
        }

        var oldestIds = await db.ContactProfileHistory
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.ContactId == contactId)
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.ContactProfileHistoryId)
            .Select(x => x.ContactProfileHistoryId)
            .Take(rowsToDelete)
            .ToListAsync(cancellationToken);

        if (oldestIds.Count == 0)
        {
            return;
        }

        var rows = oldestIds
            .Select(id => new ContactProfileHistory { ContactProfileHistoryId = id })
            .ToList();

        db.ContactProfileHistory.AttachRange(rows);
        db.ContactProfileHistory.RemoveRange(rows);
    }

    private static string BuildBatchDeduplicationKey(ContactProfileHistory row)
    {
        var previousValue = NormalizeNullable(row.PreviousValue);
        var newValue = NormalizeNullable(row.NewValue);
        var changeType = NormalizeNullable(row.ChangeType);
        var fieldName = NormalizeNullable(row.FieldName);
        var source = NormalizeNullable(row.Source);

        return string.Join(
            "|",
            row.CompanyId,
            row.ContactId,
            changeType,
            fieldName,
            source,
            previousValue,
            newValue,
            row.ChangedByUserId?.ToString() ?? string.Empty);
    }

    private static string NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
