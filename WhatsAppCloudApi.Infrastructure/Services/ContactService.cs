using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhatsAppCloudApi.Application.Interfaces;
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

    public ContactService(ApplicationDbContext db, ILogger<ContactService> logger)
    {
        _db = db;
        _logger = logger;
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

        var exists = await _db.Contacts.AnyAsync(
            c => c.CompanyId == companyId && c.PhoneNumber == normalizedPhone && c.IsActive,
            ct);
        if (exists)
            return ApiResponse<Contact>.Fail("Contact with this phone number already exists", HttpStatusCode.Conflict);

        var contact = new Contact
        {
            CompanyId = companyId,
            Name = request.Name.Trim(),
            PhoneNumber = normalizedPhone,
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant(),
            Tags = request.Tags,
            CustomFields = request.CustomFields,
            Source = request.Source,
            Notes = request.Notes,
            FirstSeenAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow
        };

        _db.Contacts.Add(contact);
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
        var phoneConflict = await _db.Contacts.AnyAsync(
            c => c.CompanyId == companyId && c.ContactId != contactId && c.PhoneNumber == normalizedPhone && c.IsActive,
            ct);
        if (phoneConflict)
            return ApiResponse<Contact>.Fail("Contact with this phone number already exists", HttpStatusCode.Conflict);

        contact.Name = request.Name.Trim();
        contact.PhoneNumber = normalizedPhone;
        contact.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();
        contact.Tags = request.Tags;
        contact.CustomFields = request.CustomFields;
        contact.Source = request.Source;
        contact.Notes = request.Notes;
        contact.LastSeenAtUtc = DateTime.UtcNow;
        contact.UpdatedAtUtc = DateTime.UtcNow;

        if (!string.Equals(previousPhoneNumber, normalizedPhone, StringComparison.Ordinal))
        {
            var conversations = await _db.Conversations
                .Where(x => x.CompanyId == companyId && x.ContactId == contactId)
                .ToListAsync(ct);

            foreach (var conversation in conversations)
            {
                conversation.ContactNumber = normalizedPhone;
                conversation.ContactName = contact.Name;
                conversation.UpdatedAtUtc = DateTime.UtcNow;
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
                .Where(c => c.CompanyId == companyId && c.IsActive)
                .Select(c => c.PhoneNumber)
                .ToListAsync(ct)).ToHashSet();

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
}
