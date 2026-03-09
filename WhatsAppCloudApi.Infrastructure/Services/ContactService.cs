using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Infrastructure.Data;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Infrastructure.Services;

public sealed class ContactService : IContactService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ContactService> _logger;

    public ContactService(ApplicationDbContext db, ILogger<ContactService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ApiResponse<PagedResult<Contact>>> GetContactsAsync(int companyId, ContactQueryParams query, CancellationToken ct)
    {
        var q = _db.Contacts.Where(c => c.CompanyId == companyId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(c => c.Name.ToLower().Contains(s) || c.PhoneNumber.Contains(s) || (c.Email != null && c.Email.ToLower().Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(query.Tag))
            q = q.Where(c => c.Tags != null && c.Tags.Contains(query.Tag));

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(c => c.CreatedAtUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return ApiResponse<PagedResult<Contact>>.Ok(new PagedResult<Contact>
        {
            Items = items, TotalCount = total, Page = query.Page, PageSize = query.PageSize
        });
    }

    public async Task<ApiResponse<Contact>> GetContactByIdAsync(int companyId, long contactId, CancellationToken ct)
    {
        var contact = await _db.Contacts.FirstOrDefaultAsync(c => c.CompanyId == companyId && c.ContactId == contactId, ct);
        return contact is null
            ? ApiResponse<Contact>.Fail("Contact not found", HttpStatusCode.NotFound)
            : ApiResponse<Contact>.Ok(contact);
    }

    public async Task<ApiResponse<Contact>> CreateContactAsync(int companyId, ContactUpsertRequest request, CancellationToken ct)
    {
        var exists = await _db.Contacts.AnyAsync(c => c.CompanyId == companyId && c.PhoneNumber == request.PhoneNumber, ct);
        if (exists)
            return ApiResponse<Contact>.Fail("Contact with this phone number already exists", HttpStatusCode.Conflict);

        var contact = new Contact
        {
            CompanyId = companyId,
            Name = request.Name,
            PhoneNumber = request.PhoneNumber,
            Email = request.Email,
            Tags = request.Tags,
            CustomFields = request.CustomFields,
            Source = request.Source,
            Notes = request.Notes,
        };

        _db.Contacts.Add(contact);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<Contact>.Ok(contact);
    }

    public async Task<ApiResponse<Contact>> UpdateContactAsync(int companyId, long contactId, ContactUpsertRequest request, CancellationToken ct)
    {
        var contact = await _db.Contacts.FirstOrDefaultAsync(c => c.CompanyId == companyId && c.ContactId == contactId, ct);
        if (contact is null)
            return ApiResponse<Contact>.Fail("Contact not found", HttpStatusCode.NotFound);

        contact.Name = request.Name;
        contact.PhoneNumber = request.PhoneNumber;
        contact.Email = request.Email;
        contact.Tags = request.Tags;
        contact.CustomFields = request.CustomFields;
        contact.Source = request.Source;
        contact.Notes = request.Notes;
        contact.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ApiResponse<Contact>.Ok(contact);
    }

    public async Task<ApiResponse<bool>> DeleteContactAsync(int companyId, long contactId, CancellationToken ct)
    {
        var contact = await _db.Contacts.FirstOrDefaultAsync(c => c.CompanyId == companyId && c.ContactId == contactId, ct);
        if (contact is null)
            return ApiResponse<bool>.Fail("Contact not found", HttpStatusCode.NotFound);

        _db.Contacts.Remove(contact);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<int>> ImportContactsAsync(int companyId, ContactImportRequest request, CancellationToken ct)
    {
        try
        {
            var csvBytes = Convert.FromBase64String(request.CsvBase64);
            var csvText = Encoding.UTF8.GetString(csvBytes);
            var lines = csvText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2)
                return ApiResponse<int>.Fail("CSV file is empty or has no data rows", HttpStatusCode.BadRequest);

            var existingNumbers = (await _db.Contacts
                .Where(c => c.CompanyId == companyId)
                .Select(c => c.PhoneNumber)
                .ToListAsync(ct)).ToHashSet();

            var contacts = new List<Contact>();
            foreach (var line in lines.Skip(1))
            {
                var cols = line.Trim().Split(',');
                if (cols.Length < 2) continue;

                var phone = cols[1].Trim().Trim('"');
                if (existingNumbers.Contains(phone)) continue;

                contacts.Add(new Contact
                {
                    CompanyId = companyId,
                    Name = cols[0].Trim().Trim('"'),
                    PhoneNumber = phone,
                    Email = cols.Length > 2 ? cols[2].Trim().Trim('"') : null,
                    Tags = cols.Length > 3 ? cols[3].Trim().Trim('"') : null,
                    Source = "csv_import",
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
