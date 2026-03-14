using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Domain.Models;
using WhatsAppCloudApi.Infrastructure.Data;
using WhatsAppCloudApi.Shared.Utilities;

namespace WhatsAppCloudApi.Infrastructure.Services;

public sealed class CustomerConversationResolver : ICustomerConversationResolver
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<CustomerConversationResolver> _logger;

    public CustomerConversationResolver(
        ApplicationDbContext dbContext,
        ILogger<CustomerConversationResolver> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ResolvedConversationContext> ResolveAsync(
        int companyId,
        string phoneNumber,
        int? whatsAppPhoneNumberId,
        string? preferredContactName = null,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPhoneNumber = PhoneNumberNormalizer.Normalize(phoneNumber)
            ?? throw new InvalidOperationException("Invalid phone number.");

        var now = DateTime.UtcNow;
        var trimmedName = string.IsNullOrWhiteSpace(preferredContactName) ? null : preferredContactName.Trim();
        var normalizedSource = string.IsNullOrWhiteSpace(source) ? "crm_resolver" : source.Trim();
        var equivalentForms = PhoneNumberNormalizer.GetEquivalentForms(normalizedPhoneNumber);

        var matchedContacts = await _dbContext.Contacts
            .Where(x => x.CompanyId == companyId && equivalentForms.Contains(x.PhoneNumber))
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.LastSeenAtUtc ?? x.UpdatedAtUtc ?? x.CreatedAtUtc)
            .ThenBy(x => x.ContactId)
            .ToListAsync(cancellationToken);

        Contact contact;

        if (matchedContacts.Count == 0)
        {
            contact = new Contact
            {
                CompanyId = companyId,
                Name = trimmedName ?? normalizedPhoneNumber,
                PhoneNumber = normalizedPhoneNumber,
                Source = normalizedSource,
                FirstSeenAtUtc = now,
                LastSeenAtUtc = now,
                CreatedAtUtc = now
            };

            _dbContext.Contacts.Add(contact);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _dbContext.ContactProfileHistory.AddRange(
                new ContactProfileHistory
                {
                    CompanyId = companyId,
                    ContactId = contact.ContactId,
                    ChangeType = "CREATED",
                    FieldName = "name",
                    NewValue = contact.Name,
                    Source = normalizedSource,
                    Notes = "Contact created by resolver.",
                    CreatedAtUtc = now
                },
                new ContactProfileHistory
                {
                    CompanyId = companyId,
                    ContactId = contact.ContactId,
                    ChangeType = "CREATED",
                    FieldName = "phone_number",
                    NewValue = contact.PhoneNumber,
                    Source = normalizedSource,
                    Notes = "Contact created by resolver.",
                    CreatedAtUtc = now
                });
        }
        else
        {
            contact = matchedContacts.FirstOrDefault(x => x.IsActive) ?? matchedContacts[0];

            if (matchedContacts.Count > 1)
            {
                var duplicates = matchedContacts.Where(x => x.ContactId != contact.ContactId).ToList();
                if (duplicates.Count > 0)
                {
                    try
                    {
                        await MergeDuplicateContactsAsync(
                            companyId,
                            contact,
                            duplicates,
                            normalizedPhoneNumber,
                            normalizedSource,
                            now,
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Duplicate contact merge failed for company {CompanyId} and phone {PhoneNumber}. Continuing without merge.",
                            companyId,
                            normalizedPhoneNumber);
                    }
                }
            }

            if (!contact.IsActive)
            {
                AddHistoryRow(
                    companyId,
                    contact.ContactId,
                    "REACTIVATED",
                    "is_active",
                    contact.IsActive ? "true" : "false",
                    "true",
                    normalizedSource,
                    "Contact was reactivated by inbound activity.");

                contact.IsActive = true;
            }

            if (!string.Equals(contact.PhoneNumber, normalizedPhoneNumber, StringComparison.Ordinal))
            {
                AddHistoryRow(
                    companyId,
                    contact.ContactId,
                    "DETAILS_UPDATED",
                    "phone_number",
                    contact.PhoneNumber,
                    normalizedPhoneNumber,
                    normalizedSource,
                    "Canonical phone normalization applied.");

                contact.PhoneNumber = normalizedPhoneNumber;
            }

            if (!string.IsNullOrWhiteSpace(trimmedName)
                && (string.IsNullOrWhiteSpace(contact.Name)
                    || string.Equals(contact.Name, contact.PhoneNumber, StringComparison.Ordinal)
                    || string.Equals(contact.Name, normalizedPhoneNumber, StringComparison.Ordinal)))
            {
                AddHistoryRow(
                    companyId,
                    contact.ContactId,
                    "DETAILS_UPDATED",
                    "name",
                    contact.Name,
                    trimmedName,
                    normalizedSource,
                    "Contact display name enriched from inbound profile.");

                contact.Name = trimmedName;
            }

            if (string.IsNullOrWhiteSpace(contact.Source))
            {
                AddHistoryRow(
                    companyId,
                    contact.ContactId,
                    "DETAILS_UPDATED",
                    "source",
                    contact.Source,
                    normalizedSource,
                    normalizedSource);

                contact.Source = normalizedSource;
            }

            contact.LastSeenAtUtc = now;
            contact.UpdatedAtUtc = now;
        }

        var conversation = await _dbContext.Conversations
            .FirstOrDefaultAsync(
                x => x.CompanyId == companyId
                    && x.ContactId == contact.ContactId
                    && x.WhatsAppPhoneNumberId == whatsAppPhoneNumberId,
                cancellationToken);

        conversation ??= await _dbContext.Conversations
            .FirstOrDefaultAsync(
                x => x.CompanyId == companyId
                    && x.ContactNumber == normalizedPhoneNumber
                    && x.WhatsAppPhoneNumberId == whatsAppPhoneNumberId,
                cancellationToken);

        if (conversation is null)
        {
            conversation = new Conversation
            {
                CompanyId = companyId,
                ContactId = contact.ContactId,
                ContactNumber = normalizedPhoneNumber,
                ContactName = contact.Name,
                WhatsAppPhoneNumberId = whatsAppPhoneNumberId,
                Status = "OPEN",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            _dbContext.Conversations.Add(conversation);
        }
        else
        {
            var changed = false;

            if (conversation.ContactId != contact.ContactId)
            {
                conversation.ContactId = contact.ContactId;
                changed = true;
            }

            if (!string.Equals(conversation.ContactNumber, normalizedPhoneNumber, StringComparison.Ordinal))
            {
                conversation.ContactNumber = normalizedPhoneNumber;
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(contact.Name)
                && !string.Equals(conversation.ContactName, contact.Name, StringComparison.Ordinal))
            {
                conversation.ContactName = contact.Name;
                changed = true;
            }

            if (changed)
            {
                conversation.UpdatedAtUtc = now;
            }
        }

        if (_dbContext.ChangeTracker.HasChanges())
        {
            await SaveChangesWithHistoryFallbackAsync(cancellationToken);
        }

        return new ResolvedConversationContext
        {
            NormalizedPhoneNumber = normalizedPhoneNumber,
            Contact = contact,
            Conversation = conversation
        };
    }

    private async Task MergeDuplicateContactsAsync(
        int companyId,
        Contact primaryContact,
        IReadOnlyCollection<Contact> duplicates,
        string normalizedPhoneNumber,
        string source,
        DateTime now,
        CancellationToken cancellationToken)
    {
        foreach (var duplicate in duplicates)
        {
            MergeContactMetadata(primaryContact, duplicate);

            await MergeConversationsForDuplicateAsync(companyId, primaryContact, duplicate, normalizedPhoneNumber, now, cancellationToken);
            await RepointContactReferencesAsync(primaryContact.ContactId, duplicate.ContactId, cancellationToken);

            AddHistoryRow(
                companyId,
                primaryContact.ContactId,
                "MERGED",
                "contact_merge",
                duplicate.ContactId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                primaryContact.ContactId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                source,
                $"Merged duplicate contact {duplicate.ContactId} ({duplicate.PhoneNumber}).");

            duplicate.IsActive = false;
            duplicate.UpdatedAtUtc = now;
        }
    }

    private async Task MergeConversationsForDuplicateAsync(
        int companyId,
        Contact primaryContact,
        Contact duplicateContact,
        string normalizedPhoneNumber,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var primaryConversations = await _dbContext.Conversations
            .Where(x => x.CompanyId == companyId && x.ContactId == primaryContact.ContactId)
            .ToListAsync(cancellationToken);

        var duplicateConversations = await _dbContext.Conversations
            .Where(x => x.CompanyId == companyId && x.ContactId == duplicateContact.ContactId)
            .ToListAsync(cancellationToken);

        foreach (var duplicateConversation in duplicateConversations)
        {
            var targetConversation = primaryConversations.FirstOrDefault(x => x.WhatsAppPhoneNumberId == duplicateConversation.WhatsAppPhoneNumberId);
            if (targetConversation is null)
            {
                duplicateConversation.ContactId = primaryContact.ContactId;
                duplicateConversation.ContactNumber = normalizedPhoneNumber;
                duplicateConversation.ContactName = primaryContact.Name;
                duplicateConversation.UpdatedAtUtc = now;
                primaryConversations.Add(duplicateConversation);
                continue;
            }

            if (targetConversation.ConversationId == duplicateConversation.ConversationId)
            {
                continue;
            }

            await MoveConversationReferencesAsync(targetConversation.ConversationId, duplicateConversation.ConversationId, cancellationToken);
            MergeConversationState(targetConversation, duplicateConversation, normalizedPhoneNumber, primaryContact.Name, now);
            _dbContext.Conversations.Remove(duplicateConversation);
        }
    }

    private async Task MoveConversationReferencesAsync(long targetConversationId, long duplicateConversationId, CancellationToken cancellationToken)
    {
        var messages = await LoadRowsSafelyAsync(
            _dbContext.ConversationMessages.Where(x => x.ConversationId == duplicateConversationId),
            nameof(_dbContext.ConversationMessages),
            cancellationToken);
        foreach (var row in messages)
        {
            row.ConversationId = targetConversationId;
        }

        var flowSessions = await LoadRowsSafelyAsync(
            _dbContext.ConversationFlowSessions.Where(x => x.ConversationId == duplicateConversationId),
            nameof(_dbContext.ConversationFlowSessions),
            cancellationToken);
        foreach (var row in flowSessions)
        {
            row.ConversationId = targetConversationId;
        }

        var executionLogs = await LoadRowsSafelyAsync(
            _dbContext.ConversationFlowExecutionLogs.Where(x => x.ConversationId == duplicateConversationId),
            nameof(_dbContext.ConversationFlowExecutionLogs),
            cancellationToken);
        foreach (var row in executionLogs)
        {
            row.ConversationId = targetConversationId;
        }

        var formSubmissions = await LoadRowsSafelyAsync(
            _dbContext.ConversationFlowFormSubmissions.Where(x => x.ConversationId == duplicateConversationId),
            nameof(_dbContext.ConversationFlowFormSubmissions),
            cancellationToken);
        foreach (var row in formSubmissions)
        {
            row.ConversationId = targetConversationId;
        }

        var leads = await LoadRowsSafelyAsync(
            _dbContext.LeadRecords.Where(x => x.ConversationId == duplicateConversationId),
            nameof(_dbContext.LeadRecords),
            cancellationToken);
        foreach (var row in leads)
        {
            row.ConversationId = targetConversationId;
        }

        var outboundMessages = await LoadRowsSafelyAsync(
            _dbContext.Messages.Where(x => x.ConversationId == duplicateConversationId),
            nameof(_dbContext.Messages),
            cancellationToken);
        foreach (var row in outboundMessages)
        {
            row.ConversationId = targetConversationId;
        }

        var assignmentHistory = await LoadRowsSafelyAsync(
            _dbContext.ConversationAssignmentHistory.Where(x => x.ConversationId == duplicateConversationId),
            nameof(_dbContext.ConversationAssignmentHistory),
            cancellationToken);
        foreach (var row in assignmentHistory)
        {
            row.ConversationId = targetConversationId;
        }
    }

    private async Task RepointContactReferencesAsync(long targetContactId, long duplicateContactId, CancellationToken cancellationToken)
    {
        var messageRows = await LoadRowsSafelyAsync(
            _dbContext.Messages.Where(x => x.ContactId == duplicateContactId),
            nameof(_dbContext.Messages),
            cancellationToken);
        foreach (var row in messageRows)
        {
            row.ContactId = targetContactId;
        }

        var campaignRows = await LoadRowsSafelyAsync(
            _dbContext.CampaignContacts.Where(x => x.ContactId == duplicateContactId),
            nameof(_dbContext.CampaignContacts),
            cancellationToken);
        foreach (var row in campaignRows)
        {
            row.ContactId = targetContactId;
        }

        var flowSessionRows = await LoadRowsSafelyAsync(
            _dbContext.ConversationFlowSessions.Where(x => x.ContactId == duplicateContactId),
            nameof(_dbContext.ConversationFlowSessions),
            cancellationToken);
        foreach (var row in flowSessionRows)
        {
            row.ContactId = targetContactId;
        }

        var logRows = await LoadRowsSafelyAsync(
            _dbContext.ConversationFlowExecutionLogs.Where(x => x.ContactId == duplicateContactId),
            nameof(_dbContext.ConversationFlowExecutionLogs),
            cancellationToken);
        foreach (var row in logRows)
        {
            row.ContactId = targetContactId;
        }

        var formRows = await LoadRowsSafelyAsync(
            _dbContext.ConversationFlowFormSubmissions.Where(x => x.ContactId == duplicateContactId),
            nameof(_dbContext.ConversationFlowFormSubmissions),
            cancellationToken);
        foreach (var row in formRows)
        {
            row.ContactId = targetContactId;
        }

        var leadRows = await LoadRowsSafelyAsync(
            _dbContext.LeadRecords.Where(x => x.ContactId == duplicateContactId),
            nameof(_dbContext.LeadRecords),
            cancellationToken);
        foreach (var row in leadRows)
        {
            row.ContactId = targetContactId;
        }

        var historyRows = await LoadRowsSafelyAsync(
            _dbContext.ConversationAssignmentHistory.Where(x => x.ContactId == duplicateContactId),
            nameof(_dbContext.ConversationAssignmentHistory),
            cancellationToken);
        foreach (var row in historyRows)
        {
            row.ContactId = targetContactId;
        }
    }

    private static void MergeConversationState(
        Conversation target,
        Conversation duplicate,
        string normalizedPhoneNumber,
        string contactName,
        DateTime now)
    {
        target.UnreadCount += duplicate.UnreadCount;

        if ((duplicate.LastMessageAtUtc ?? duplicate.CreatedAtUtc) > (target.LastMessageAtUtc ?? target.CreatedAtUtc))
        {
            target.LastMessageAtUtc = duplicate.LastMessageAtUtc;
            target.LastMessageContent = duplicate.LastMessageContent;
            target.LastMessageType = duplicate.LastMessageType;
            target.LastInboundMessageAtUtc = duplicate.LastInboundMessageAtUtc;
        }

        if (target.AssignedUserId is null && duplicate.AssignedUserId.HasValue)
        {
            target.AssignedUserId = duplicate.AssignedUserId;
        }

        if (target.AssignedTeamId is null && duplicate.AssignedTeamId.HasValue)
        {
            target.AssignedTeamId = duplicate.AssignedTeamId;
        }

        if (string.IsNullOrWhiteSpace(target.Status) && !string.IsNullOrWhiteSpace(duplicate.Status))
        {
            target.Status = duplicate.Status;
        }

        target.ContactNumber = normalizedPhoneNumber;
        target.ContactName = contactName;
        target.UpdatedAtUtc = now;
    }

    private static void MergeContactMetadata(Contact primary, Contact duplicate)
    {
        if (string.IsNullOrWhiteSpace(primary.Email) && !string.IsNullOrWhiteSpace(duplicate.Email))
        {
            primary.Email = duplicate.Email;
        }

        if (string.IsNullOrWhiteSpace(primary.Tags) && !string.IsNullOrWhiteSpace(duplicate.Tags))
        {
            primary.Tags = duplicate.Tags;
        }

        if (string.IsNullOrWhiteSpace(primary.CustomFields) && !string.IsNullOrWhiteSpace(duplicate.CustomFields))
        {
            primary.CustomFields = duplicate.CustomFields;
        }

        if (string.IsNullOrWhiteSpace(primary.Source) && !string.IsNullOrWhiteSpace(duplicate.Source))
        {
            primary.Source = duplicate.Source;
        }

        if (string.IsNullOrWhiteSpace(primary.Notes) && !string.IsNullOrWhiteSpace(duplicate.Notes))
        {
            primary.Notes = duplicate.Notes;
        }

        if (!primary.OwnerUserId.HasValue && duplicate.OwnerUserId.HasValue)
        {
            primary.OwnerUserId = duplicate.OwnerUserId;
            primary.OwnerAssignedAtUtc = duplicate.OwnerAssignedAtUtc;
        }

        if (duplicate.FirstSeenAtUtc < primary.FirstSeenAtUtc)
        {
            primary.FirstSeenAtUtc = duplicate.FirstSeenAtUtc;
        }

        if (duplicate.LastSeenAtUtc.HasValue
            && (!primary.LastSeenAtUtc.HasValue || duplicate.LastSeenAtUtc > primary.LastSeenAtUtc))
        {
            primary.LastSeenAtUtc = duplicate.LastSeenAtUtc;
        }

        if (duplicate.LastInboundMessageAtUtc.HasValue
            && (!primary.LastInboundMessageAtUtc.HasValue || duplicate.LastInboundMessageAtUtc > primary.LastInboundMessageAtUtc))
        {
            primary.LastInboundMessageAtUtc = duplicate.LastInboundMessageAtUtc;
        }

        if (duplicate.LastOutboundMessageAtUtc.HasValue
            && (!primary.LastOutboundMessageAtUtc.HasValue || duplicate.LastOutboundMessageAtUtc > primary.LastOutboundMessageAtUtc))
        {
            primary.LastOutboundMessageAtUtc = duplicate.LastOutboundMessageAtUtc;
        }
    }

    private void AddHistoryRow(
        int companyId,
        long contactId,
        string changeType,
        string fieldName,
        string? previousValue,
        string? newValue,
        string source,
        string? notes = null)
    {
        var normalizedPrevious = NormalizeHistoryValue(previousValue);
        var normalizedNew = NormalizeHistoryValue(newValue);
        if (string.Equals(normalizedPrevious, normalizedNew, StringComparison.Ordinal))
        {
            return;
        }

        _dbContext.ContactProfileHistory.Add(new ContactProfileHistory
        {
            CompanyId = companyId,
            ContactId = contactId,
            ChangeType = changeType,
            FieldName = fieldName,
            PreviousValue = normalizedPrevious,
            NewValue = normalizedNew,
            Source = source,
            Notes = notes,
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    private static string? NormalizeHistoryValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private async Task SaveChangesWithHistoryFallbackAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (TryDetachPendingHistoryEntries(out var detachedCount))
        {
            _logger.LogWarning(
                ex,
                "Contact profile history persistence failed; continuing without history rows. Detached {DetachedCount} history entries.",
                detachedCount);

            if (_dbContext.ChangeTracker.HasChanges())
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }

    private bool TryDetachPendingHistoryEntries(out int detachedCount)
    {
        var pendingHistoryEntries = _dbContext.ChangeTracker.Entries<ContactProfileHistory>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        detachedCount = pendingHistoryEntries.Count;
        if (detachedCount == 0)
        {
            return false;
        }

        foreach (var entry in pendingHistoryEntries)
        {
            entry.State = EntityState.Detached;
        }

        return true;
    }

    private async Task<List<T>> LoadRowsSafelyAsync<T>(
        IQueryable<T> query,
        string tableName,
        CancellationToken cancellationToken)
    {
        try
        {
            return await query.ToListAsync(cancellationToken);
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            _logger.LogWarning(
                ex,
                "Skipping duplicate-contact merge reference update for missing table {TableName}.",
                tableName);
            return [];
        }
    }
}
