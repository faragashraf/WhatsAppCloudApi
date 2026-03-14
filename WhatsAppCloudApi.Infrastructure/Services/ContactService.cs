using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.Configuration;
using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Infrastructure.Data;
using WhatsAppCloudApi.Shared.Responses;
using WhatsAppCloudApi.Shared.Utilities;

namespace WhatsAppCloudApi.Infrastructure.Services;

public sealed class ContactService : IContactService
{
    private const int MaxImportRows = 5000;

    private readonly ApplicationDbContext _db;
    private readonly ILogger<ContactService> _logger;
    private readonly ContactProfileHistoryOptions _contactProfileHistoryOptions;

    public ContactService(
        ApplicationDbContext db,
        ILogger<ContactService> logger,
        IOptionsSnapshot<ContactProfileHistoryOptions> contactProfileHistoryOptions)
    {
        _db = db;
        _logger = logger;
        _contactProfileHistoryOptions = contactProfileHistoryOptions.Value;
    }

    public async Task<ApiResponse<PagedResult<Contact>>> GetContactsAsync(int companyId, ContactQueryParams query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var q = _db.Contacts
            .Include(c => c.OwnerUser)
            .Where(c => c.CompanyId == companyId && c.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(c => c.Name.ToLower().Contains(s) || c.PhoneNumber.Contains(s) || (c.Email != null && c.Email.ToLower().Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(query.Tag))
            q = q.Where(c => c.Tags != null && c.Tags.Contains(query.Tag));

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(c => c.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return ApiResponse<PagedResult<Contact>>.Ok(new PagedResult<Contact>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<ApiResponse<Contact>> GetContactByIdAsync(int companyId, long contactId, CancellationToken ct)
    {
        var contact = await _db.Contacts
            .Include(c => c.OwnerUser)
            .FirstOrDefaultAsync(c => c.CompanyId == companyId && c.ContactId == contactId && c.IsActive, ct);

        return contact is null
            ? ApiResponse<Contact>.Fail("Contact not found", HttpStatusCode.NotFound)
            : ApiResponse<Contact>.Ok(contact);
    }

    public async Task<ApiResponse<Contact>> CreateContactAsync(int companyId, ContactUpsertRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ApiResponse<Contact>.Fail("Name is required.", HttpStatusCode.BadRequest);

        var normalizedPhone = PhoneNumberNormalizer.Normalize(request.PhoneNumber);
        if (normalizedPhone is null)
            return ApiResponse<Contact>.Fail("Invalid phone number format.", HttpStatusCode.BadRequest);

        var equivalentForms = PhoneNumberNormalizer.GetEquivalentForms(normalizedPhone);
        var equivalentContacts = await _db.Contacts
            .Where(c => c.CompanyId == companyId && equivalentForms.Contains(c.PhoneNumber))
            .OrderByDescending(c => c.IsActive)
            .ThenBy(c => c.ContactId)
            .ToListAsync(ct);

        var activeMatch = equivalentContacts.FirstOrDefault(c => c.IsActive);
        if (activeMatch is not null)
            return ApiResponse<Contact>.Fail("Contact with this phone number already exists", HttpStatusCode.Conflict);

        var now = DateTime.UtcNow;
        var normalizedEmail = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();

        var inactiveMatch = equivalentContacts.FirstOrDefault();
        if (inactiveMatch is not null)
        {
            var historyRows = new List<ContactProfileHistory>();
            AddHistoryRow(historyRows, companyId, inactiveMatch.ContactId, "REACTIVATED", "is_active", inactiveMatch.IsActive ? "true" : "false", "true", "contacts_api", notes: "Contact reactivated by create request.");
            AddHistoryRow(historyRows, companyId, inactiveMatch.ContactId, "DETAILS_UPDATED", "name", inactiveMatch.Name, request.Name.Trim(), "contacts_api");
            AddHistoryRow(historyRows, companyId, inactiveMatch.ContactId, "DETAILS_UPDATED", "phone_number", inactiveMatch.PhoneNumber, normalizedPhone, "contacts_api");
            AddHistoryRow(historyRows, companyId, inactiveMatch.ContactId, "DETAILS_UPDATED", "email", inactiveMatch.Email, normalizedEmail, "contacts_api");
            AddHistoryRow(historyRows, companyId, inactiveMatch.ContactId, "DETAILS_UPDATED", "tags", inactiveMatch.Tags, request.Tags, "contacts_api");
            AddHistoryRow(historyRows, companyId, inactiveMatch.ContactId, "DETAILS_UPDATED", "custom_fields", inactiveMatch.CustomFields, request.CustomFields, "contacts_api");
            AddHistoryRow(historyRows, companyId, inactiveMatch.ContactId, "DETAILS_UPDATED", "source", inactiveMatch.Source, request.Source, "contacts_api");
            AddHistoryRow(historyRows, companyId, inactiveMatch.ContactId, "DETAILS_UPDATED", "notes", inactiveMatch.Notes, request.Notes, "contacts_api");

            inactiveMatch.IsActive = true;
            inactiveMatch.Name = request.Name.Trim();
            inactiveMatch.PhoneNumber = normalizedPhone;
            inactiveMatch.Email = normalizedEmail;
            inactiveMatch.Tags = request.Tags;
            inactiveMatch.CustomFields = request.CustomFields;
            inactiveMatch.Source = request.Source;
            inactiveMatch.Notes = request.Notes;
            inactiveMatch.LastSeenAtUtc = now;
            inactiveMatch.UpdatedAtUtc = now;

            if (inactiveMatch.FirstSeenAtUtc == default)
            {
                inactiveMatch.FirstSeenAtUtc = now;
            }

            if (historyRows.Count > 0)
            {
                var compactedRows = ContactProfileHistoryMaintenance.CompactPendingRows(historyRows, _contactProfileHistoryOptions);
                if (compactedRows.Count > 0)
                {
                    _db.ContactProfileHistory.AddRange(compactedRows);
                    await ContactProfileHistoryMaintenance.EnforcePerContactLimitAsync(
                        _db,
                        companyId,
                        inactiveMatch.ContactId,
                        _contactProfileHistoryOptions,
                        ct);
                }
            }

            await _db.SaveChangesAsync(ct);
            return ApiResponse<Contact>.Ok(inactiveMatch, "Existing contact reactivated.");
        }

        var contact = new Contact
        {
            CompanyId = companyId,
            Name = request.Name.Trim(),
            PhoneNumber = normalizedPhone,
            Email = normalizedEmail,
            Tags = request.Tags,
            CustomFields = request.CustomFields,
            Source = request.Source,
            Notes = request.Notes,
            FirstSeenAtUtc = now,
            LastSeenAtUtc = now
        };

        _db.Contacts.Add(contact);
        await _db.SaveChangesAsync(ct);

        var createdHistoryRows = ContactProfileHistoryMaintenance.CompactPendingRows(
        [
            new ContactProfileHistory
            {
                CompanyId = companyId,
                ContactId = contact.ContactId,
                ChangeType = "CREATED",
                FieldName = "name",
                NewValue = contact.Name,
                Source = "contacts_api",
                Notes = "Contact created.",
                CreatedAtUtc = now
            },
            new ContactProfileHistory
            {
                CompanyId = companyId,
                ContactId = contact.ContactId,
                ChangeType = "CREATED",
                FieldName = "phone_number",
                NewValue = contact.PhoneNumber,
                Source = "contacts_api",
                Notes = "Contact created.",
                CreatedAtUtc = now
            }
        ], _contactProfileHistoryOptions);

        _db.ContactProfileHistory.AddRange(createdHistoryRows);
        await ContactProfileHistoryMaintenance.EnforcePerContactLimitAsync(
            _db,
            companyId,
            contact.ContactId,
            _contactProfileHistoryOptions,
            ct);
        await _db.SaveChangesAsync(ct);

        return ApiResponse<Contact>.Ok(contact);
    }

    public async Task<ApiResponse<Contact>> UpdateContactAsync(int companyId, long contactId, ContactUpsertRequest request, CancellationToken ct)
    {
        var contact = await _db.Contacts
            .FirstOrDefaultAsync(c => c.CompanyId == companyId && c.ContactId == contactId && c.IsActive, ct);
        if (contact is null)
            return ApiResponse<Contact>.Fail("Contact not found", HttpStatusCode.NotFound);

        if (string.IsNullOrWhiteSpace(request.Name))
            return ApiResponse<Contact>.Fail("Name is required.", HttpStatusCode.BadRequest);

        var normalizedPhone = PhoneNumberNormalizer.Normalize(request.PhoneNumber);
        if (normalizedPhone is null)
            return ApiResponse<Contact>.Fail("Invalid phone number format.", HttpStatusCode.BadRequest);

        var previousPhoneNumber = contact.PhoneNumber;
        var equivalentForms = PhoneNumberNormalizer.GetEquivalentForms(normalizedPhone);
        var phoneConflict = await _db.Contacts.AnyAsync(
            c => c.CompanyId == companyId && c.ContactId != contactId && equivalentForms.Contains(c.PhoneNumber),
            ct);
        if (phoneConflict)
            return ApiResponse<Contact>.Fail("Contact with this phone number already exists", HttpStatusCode.Conflict);

        var normalizedEmail = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow;

        var historyRows = new List<ContactProfileHistory>();
        AddHistoryRow(historyRows, companyId, contact.ContactId, "DETAILS_UPDATED", "name", contact.Name, request.Name.Trim(), "contacts_api");
        AddHistoryRow(historyRows, companyId, contact.ContactId, "DETAILS_UPDATED", "phone_number", contact.PhoneNumber, normalizedPhone, "contacts_api");
        AddHistoryRow(historyRows, companyId, contact.ContactId, "DETAILS_UPDATED", "email", contact.Email, normalizedEmail, "contacts_api");
        AddHistoryRow(historyRows, companyId, contact.ContactId, "DETAILS_UPDATED", "tags", contact.Tags, request.Tags, "contacts_api");
        AddHistoryRow(historyRows, companyId, contact.ContactId, "DETAILS_UPDATED", "custom_fields", contact.CustomFields, request.CustomFields, "contacts_api");
        AddHistoryRow(historyRows, companyId, contact.ContactId, "DETAILS_UPDATED", "source", contact.Source, request.Source, "contacts_api");
        AddHistoryRow(historyRows, companyId, contact.ContactId, "DETAILS_UPDATED", "notes", contact.Notes, request.Notes, "contacts_api");

        contact.Name = request.Name.Trim();
        contact.PhoneNumber = normalizedPhone;
        contact.Email = normalizedEmail;
        contact.Tags = request.Tags;
        contact.CustomFields = request.CustomFields;
        contact.Source = request.Source;
        contact.Notes = request.Notes;
        contact.LastSeenAtUtc = now;
        contact.UpdatedAtUtc = now;

        if (!string.Equals(previousPhoneNumber, normalizedPhone, StringComparison.Ordinal))
        {
            var conversations = await _db.Conversations
                .Where(x => x.CompanyId == companyId && x.ContactId == contactId)
                .ToListAsync(ct);

            foreach (var conversation in conversations)
            {
                conversation.ContactNumber = normalizedPhone;
                conversation.ContactName = contact.Name;
                conversation.UpdatedAtUtc = now;
            }
        }

        if (historyRows.Count > 0)
        {
            var compactedRows = ContactProfileHistoryMaintenance.CompactPendingRows(historyRows, _contactProfileHistoryOptions);
            if (compactedRows.Count > 0)
            {
                _db.ContactProfileHistory.AddRange(compactedRows);
                await ContactProfileHistoryMaintenance.EnforcePerContactLimitAsync(
                    _db,
                    companyId,
                    contact.ContactId,
                    _contactProfileHistoryOptions,
                    ct);
            }
        }

        await _db.SaveChangesAsync(ct);
        return ApiResponse<Contact>.Ok(contact);
    }

    public async Task<ApiResponse<bool>> DeleteContactAsync(int companyId, long contactId, CancellationToken ct)
    {
        var contact = await _db.Contacts
            .FirstOrDefaultAsync(c => c.CompanyId == companyId && c.ContactId == contactId && c.IsActive, ct);
        if (contact is null)
            return ApiResponse<bool>.Fail("Contact not found", HttpStatusCode.NotFound);

        contact.IsActive = false;
        contact.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<int>> ImportContactsAsync(int companyId, ContactImportRequest request, CancellationToken ct)
    {
        try
        {
            if (request.CsvBase64.Length > 2_800_000)
            {
                return ApiResponse<int>.Fail("CSV payload is too large.", HttpStatusCode.BadRequest);
            }

            var csvBytes = Convert.FromBase64String(request.CsvBase64);
            if (csvBytes.Length > 2 * 1024 * 1024)
            {
                return ApiResponse<int>.Fail("CSV file exceeds 2MB.", HttpStatusCode.BadRequest);
            }

            var csvText = Encoding.UTF8.GetString(csvBytes);
            var lines = csvText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2)
                return ApiResponse<int>.Fail("CSV file is empty or has no data rows", HttpStatusCode.BadRequest);

            if (lines.Length > MaxImportRows + 1)
                return ApiResponse<int>.Fail($"CSV row limit exceeded. Maximum {MaxImportRows} rows per import.", HttpStatusCode.BadRequest);

            var existingNumbers = (await _db.Contacts
                .Where(c => c.CompanyId == companyId)
                .Select(c => c.PhoneNumber)
                .ToListAsync(ct))
                .Select(PhoneNumberNormalizer.Normalize)
                .Where(x => x is not null)
                .Select(x => x!)
                .ToHashSet(StringComparer.Ordinal);

            var contacts = new List<Contact>();
            foreach (var line in lines.Skip(1))
            {
                var cols = line.Trim().Split(',');
                if (cols.Length < 2) continue;

                var phone = PhoneNumberNormalizer.Normalize(cols[1].Trim().Trim('"'));
                if (phone is null)
                {
                    continue;
                }

                if (existingNumbers.Contains(phone)) continue;

                contacts.Add(new Contact
                {
                    CompanyId = companyId,
                    Name = cols[0].Trim().Trim('"'),
                    PhoneNumber = phone,
                    Email = cols.Length > 2 ? cols[2].Trim().Trim('"') : null,
                    Tags = cols.Length > 3 ? cols[3].Trim().Trim('"') : null,
                    Source = "csv_import",
                    FirstSeenAtUtc = DateTime.UtcNow,
                    LastSeenAtUtc = DateTime.UtcNow
                });
                existingNumbers.Add(phone);
            }

            _db.Contacts.AddRange(contacts);
            await _db.SaveChangesAsync(ct);
            return ApiResponse<int>.Ok(contacts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import contacts for company {CompanyId}", companyId);
            return ApiResponse<int>.Fail("Import failed: " + ex.Message, HttpStatusCode.BadRequest);
        }
    }

    private static void AddHistoryRow(
        ICollection<ContactProfileHistory> rows,
        int companyId,
        long contactId,
        string changeType,
        string fieldName,
        string? previousValue,
        string? newValue,
        string source,
        int? changedByUserId = null,
        string? notes = null)
    {
        var normalizedPrevious = NormalizeHistoryValue(previousValue);
        var normalizedNew = NormalizeHistoryValue(newValue);
        if (string.Equals(normalizedPrevious, normalizedNew, StringComparison.Ordinal))
        {
            return;
        }

        rows.Add(new ContactProfileHistory
        {
            CompanyId = companyId,
            ContactId = contactId,
            ChangedByUserId = changedByUserId,
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
}
