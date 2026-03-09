using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Infrastructure.Data;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Infrastructure.Services;

public sealed class CampaignService : ICampaignService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<CampaignService> _logger;

    public CampaignService(ApplicationDbContext db, ILogger<CampaignService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ApiResponse<PagedResult<Campaign>>> GetCampaignsAsync(int companyId, CampaignQueryParams query, CancellationToken ct)
    {
        var q = _db.Campaigns.Where(c => c.CompanyId == companyId);

        if (!string.IsNullOrWhiteSpace(query.Status))
            q = q.Where(c => c.Status == query.Status);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(c => c.Name.ToLower().Contains(s));
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(c => c.CreatedAtUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return ApiResponse<PagedResult<Campaign>>.Ok(new PagedResult<Campaign>
        {
            Items = items, TotalCount = total, Page = query.Page, PageSize = query.PageSize
        });
    }

    public async Task<ApiResponse<Campaign>> GetCampaignByIdAsync(int companyId, long campaignId, CancellationToken ct)
    {
        var campaign = await _db.Campaigns
            .Include(c => c.CampaignContacts)
            .FirstOrDefaultAsync(c => c.CompanyId == companyId && c.CampaignId == campaignId, ct);

        return campaign is null
            ? ApiResponse<Campaign>.Fail("Campaign not found", HttpStatusCode.NotFound)
            : ApiResponse<Campaign>.Ok(campaign);
    }

    public async Task<ApiResponse<Campaign>> CreateCampaignAsync(int companyId, CampaignCreateRequest request, CancellationToken ct)
    {
        var campaign = new Campaign
        {
            CompanyId = companyId,
            Name = request.Name,
            Description = request.Description,
            TemplateName = request.TemplateName,
            LanguageCode = request.LanguageCode,
            TemplateParametersJson = request.TemplateParametersJson,
            WhatsAppPhoneNumberId = request.WhatsAppPhoneNumberId,
            ScheduledAtUtc = request.ScheduledAtUtc,
            Status = request.ScheduledAtUtc.HasValue ? "SCHEDULED" : "DRAFT",
        };

        // Add contacts from phone numbers list
        var allPhones = new HashSet<string>(request.PhoneNumbers);

        // Add contacts from ContactIds
        if (request.ContactIds.Count > 0)
        {
            var contactPhones = await _db.Contacts
                .Where(c => c.CompanyId == companyId && request.ContactIds.Contains(c.ContactId))
                .Select(c => c.PhoneNumber)
                .ToListAsync(ct);
            foreach (var p in contactPhones) allPhones.Add(p);
        }

        campaign.TotalContacts = allPhones.Count;

        foreach (var phone in allPhones)
        {
            var contactId = await _db.Contacts
                .Where(c => c.CompanyId == companyId && c.PhoneNumber == phone)
                .Select(c => (long?)c.ContactId)
                .FirstOrDefaultAsync(ct);

            campaign.CampaignContacts.Add(new CampaignContact
            {
                PhoneNumber = phone,
                ContactId = contactId,
            });
        }

        _db.Campaigns.Add(campaign);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<Campaign>.Ok(campaign);
    }

    public async Task<ApiResponse<Campaign>> UpdateCampaignAsync(int companyId, long campaignId, CampaignUpdateRequest request, CancellationToken ct)
    {
        var campaign = await _db.Campaigns.FirstOrDefaultAsync(c => c.CompanyId == companyId && c.CampaignId == campaignId, ct);
        if (campaign is null)
            return ApiResponse<Campaign>.Fail("Campaign not found", HttpStatusCode.NotFound);

        if (campaign.Status is not ("DRAFT" or "SCHEDULED"))
            return ApiResponse<Campaign>.Fail("Cannot update a running or completed campaign", HttpStatusCode.BadRequest);

        if (request.Name is not null) campaign.Name = request.Name;
        if (request.Description is not null) campaign.Description = request.Description;
        if (request.ScheduledAtUtc.HasValue)
        {
            campaign.ScheduledAtUtc = request.ScheduledAtUtc;
            campaign.Status = "SCHEDULED";
        }
        campaign.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ApiResponse<Campaign>.Ok(campaign);
    }

    public async Task<ApiResponse<bool>> CancelCampaignAsync(int companyId, long campaignId, CancellationToken ct)
    {
        var campaign = await _db.Campaigns.FirstOrDefaultAsync(c => c.CompanyId == companyId && c.CampaignId == campaignId, ct);
        if (campaign is null)
            return ApiResponse<bool>.Fail("Campaign not found", HttpStatusCode.NotFound);

        if (campaign.Status is "COMPLETED" or "CANCELLED")
            return ApiResponse<bool>.Fail("Campaign already finished", HttpStatusCode.BadRequest);

        campaign.Status = "CANCELLED";
        campaign.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<bool>> LaunchCampaignAsync(int companyId, long campaignId, CancellationToken ct)
    {
        var campaign = await _db.Campaigns
            .Include(c => c.CampaignContacts)
            .FirstOrDefaultAsync(c => c.CompanyId == companyId && c.CampaignId == campaignId, ct);

        if (campaign is null)
            return ApiResponse<bool>.Fail("Campaign not found", HttpStatusCode.NotFound);

        if (campaign.Status is not ("DRAFT" or "SCHEDULED"))
            return ApiResponse<bool>.Fail("Campaign cannot be launched in current state", HttpStatusCode.BadRequest);

        campaign.Status = "RUNNING";
        campaign.StartedAtUtc = DateTime.UtcNow;
        campaign.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Campaign {CampaignId} launched with {Count} contacts", campaignId, campaign.TotalContacts);
        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<PagedResult<CampaignContact>>> GetCampaignContactsAsync(int companyId, long campaignId, int page, int pageSize, CancellationToken ct)
    {
        var exists = await _db.Campaigns.AnyAsync(c => c.CompanyId == companyId && c.CampaignId == campaignId, ct);
        if (!exists)
            return ApiResponse<PagedResult<CampaignContact>>.Fail("Campaign not found", HttpStatusCode.NotFound);

        var q = _db.CampaignContacts.Where(cc => cc.CampaignId == campaignId);
        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(cc => cc.CampaignContactId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return ApiResponse<PagedResult<CampaignContact>>.Ok(new PagedResult<CampaignContact>
        {
            Items = items, TotalCount = total, Page = page, PageSize = pageSize
        });
    }
}
