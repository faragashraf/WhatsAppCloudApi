using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Domain.Models;
using WhatsAppCloudApi.Infrastructure.Data;
using WhatsAppCloudApi.Shared.Responses;
using WhatsAppCloudApi.Shared.Utilities;

namespace WhatsAppCloudApi.Infrastructure.Services;

public sealed class CampaignService : ICampaignService
{
    private const int ContactLookupBatchSize = 100;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _db;
    private readonly ILogger<CampaignService> _logger;
    private readonly ITenantWhatsAppConfigService _tenantWhatsAppConfigService;
    private readonly IWhatsAppGraphClient _whatsAppGraphClient;
    private readonly IWhatsAppService _whatsAppService;
    private readonly ISubscriptionValidationService _subscriptionValidationService;

    public CampaignService(
        ApplicationDbContext db,
        ILogger<CampaignService> logger,
        ITenantWhatsAppConfigService tenantWhatsAppConfigService,
        IWhatsAppGraphClient whatsAppGraphClient,
        IWhatsAppService whatsAppService,
        ISubscriptionValidationService subscriptionValidationService)
    {
        _db = db;
        _logger = logger;
        _tenantWhatsAppConfigService = tenantWhatsAppConfigService;
        _whatsAppGraphClient = whatsAppGraphClient;
        _whatsAppService = whatsAppService;
        _subscriptionValidationService = subscriptionValidationService;
    }

    public async Task<ApiResponse<PagedResult<Campaign>>> GetCampaignsAsync(int companyId, CampaignQueryParams query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var q = _db.Campaigns.Where(c => c.CompanyId == companyId);

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            q = q.Where(c => c.Status == query.Status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLowerInvariant();
            q = q.Where(c => c.Name.ToLower().Contains(s));
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(c => c.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return ApiResponse<PagedResult<Campaign>>.Ok(new PagedResult<Campaign>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
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

    public async Task<ApiResponse<CampaignRecipientValidationResultDto>> ValidateRecipientsAsync(
        int companyId,
        CampaignRecipientValidationRequest request,
        CancellationToken ct)
    {
        var result = await ValidateRecipientsCoreAsync(companyId, request, ct);
        return ApiResponse<CampaignRecipientValidationResultDto>.Ok(result);
    }

    public async Task<ApiResponse<Campaign>> CreateCampaignAsync(int companyId, CampaignCreateRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.TemplateName))
        {
            return ApiResponse<Campaign>.Fail("Campaign name and template name are required.", HttpStatusCode.BadRequest);
        }

        var validation = await ValidateRecipientsCoreAsync(
            companyId,
            new CampaignRecipientValidationRequest
            {
                WhatsAppPhoneNumberId = request.WhatsAppPhoneNumberId,
                PhoneNumbers = request.PhoneNumbers ?? [],
                ContactIds = request.ContactIds ?? []
            },
            ct);

        if (validation.TotalRecipients == 0)
        {
            return ApiResponse<Campaign>.Fail("At least one valid recipient is required.", HttpStatusCode.BadRequest);
        }

        var now = DateTime.UtcNow;
        var campaign = new Campaign
        {
            CompanyId = companyId,
            Name = request.Name.Trim(),
            Description = request.Description,
            TemplateName = request.TemplateName.Trim(),
            LanguageCode = string.IsNullOrWhiteSpace(request.LanguageCode) ? "ar" : request.LanguageCode.Trim(),
            TemplateParametersJson = request.TemplateParametersJson,
            WhatsAppPhoneNumberId = request.WhatsAppPhoneNumberId,
            ScheduledAtUtc = request.ScheduledAtUtc,
            Status = request.ScheduledAtUtc.HasValue ? "SCHEDULED" : "DRAFT",
            CreatedAtUtc = now
        };

        foreach (var recipient in validation.Recipients.Where(x => !string.IsNullOrWhiteSpace(x.PhoneNumber)))
        {
            campaign.CampaignContacts.Add(new CampaignContact
            {
                ContactId = recipient.ContactId,
                PhoneNumber = recipient.PhoneNumber,
                Status = recipient.IsWhatsAppAccountConfirmed ? "PENDING" : "SKIPPED",
                IsWhatsAppAccountConfirmed = recipient.IsWhatsAppAccountConfirmed,
                WhatsAppLookupStatus = recipient.Status,
                WhatsAppLookupWaId = recipient.WaId,
                WhatsAppLookupError = recipient.Error,
                WhatsAppLookupCheckedAtUtc = now,
                FailureReason = recipient.IsWhatsAppAccountConfirmed
                    ? null
                    : recipient.Error ?? "Recipient is not a WhatsApp account.",
                CreatedAtUtc = now
            });
        }

        campaign.TotalContacts = campaign.CampaignContacts.Count;
        if (campaign.TotalContacts == 0)
        {
            return ApiResponse<Campaign>.Fail("At least one valid recipient is required.", HttpStatusCode.BadRequest);
        }

        _db.Campaigns.Add(campaign);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<Campaign>.Ok(campaign);
    }

    public async Task<ApiResponse<Campaign>> UpdateCampaignAsync(int companyId, long campaignId, CampaignUpdateRequest request, CancellationToken ct)
    {
        var campaign = await _db.Campaigns.FirstOrDefaultAsync(c => c.CompanyId == companyId && c.CampaignId == campaignId, ct);
        if (campaign is null)
        {
            return ApiResponse<Campaign>.Fail("Campaign not found", HttpStatusCode.NotFound);
        }

        if (campaign.Status is not ("DRAFT" or "SCHEDULED"))
        {
            return ApiResponse<Campaign>.Fail("Cannot update a running or completed campaign", HttpStatusCode.BadRequest);
        }

        if (request.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return ApiResponse<Campaign>.Fail("Campaign name cannot be empty.", HttpStatusCode.BadRequest);
            }

            campaign.Name = request.Name.Trim();
        }

        if (request.Description is not null)
        {
            campaign.Description = request.Description;
        }

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
        {
            return ApiResponse<bool>.Fail("Campaign not found", HttpStatusCode.NotFound);
        }

        if (campaign.Status is "COMPLETED" or "CANCELLED")
        {
            return ApiResponse<bool>.Fail("Campaign already finished", HttpStatusCode.BadRequest);
        }

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
        {
            return ApiResponse<bool>.Fail("Campaign not found", HttpStatusCode.NotFound);
        }

        if (campaign.Status is not ("DRAFT" or "SCHEDULED"))
        {
            return ApiResponse<bool>.Fail("Campaign cannot be launched in current state", HttpStatusCode.BadRequest);
        }

        var eligibleContacts = campaign.CampaignContacts
            .Where(x => x.IsWhatsAppAccountConfirmed)
            .ToList();

        if (eligibleContacts.Count == 0)
        {
            return ApiResponse<bool>.Fail("No confirmed WhatsApp recipients were found for this campaign.", HttpStatusCode.BadRequest);
        }

        await _subscriptionValidationService.ValidateCanSendMessagesAsync(companyId, eligibleContacts.Count, ct);

        string? explicitPhoneNumberId = null;
        if (campaign.WhatsAppPhoneNumberId.HasValue)
        {
            explicitPhoneNumberId = await _db.WhatsAppPhoneNumbers
                .AsNoTracking()
                .Where(p =>
                    p.CompanyId == companyId
                    && p.WhatsAppPhoneNumberId == campaign.WhatsAppPhoneNumberId.Value
                    && p.IsActive)
                .Select(p => p.PhoneNumberId)
                .FirstOrDefaultAsync(ct);

            if (string.IsNullOrWhiteSpace(explicitPhoneNumberId))
            {
                return ApiResponse<bool>.Fail("Selected WhatsApp phone number is invalid for this company.", HttpStatusCode.BadRequest);
            }
        }

        var components = ParseTemplateComponents(campaign.TemplateParametersJson);
        var now = DateTime.UtcNow;

        campaign.Status = "RUNNING";
        campaign.StartedAtUtc = now;
        campaign.UpdatedAtUtc = now;

        var queued = 0;
        var failed = 0;

        foreach (var skipped in campaign.CampaignContacts.Where(x => !x.IsWhatsAppAccountConfirmed))
        {
            skipped.Status = "SKIPPED";
            skipped.FailureReason ??= skipped.WhatsAppLookupError ?? "Recipient is not a WhatsApp account.";
        }

        foreach (var contact in eligibleContacts)
        {
            try
            {
                var response = await _whatsAppService.SendTemplateMessageAsync(
                    new SendTemplateMessageRequest
                    {
                        To = contact.PhoneNumber,
                        TemplateName = campaign.TemplateName,
                        LanguageCode = campaign.LanguageCode,
                        Components = components,
                        PhoneNumberId = explicitPhoneNumberId
                    },
                    ct);

                if (response.Success)
                {
                    contact.Status = "QUEUED";
                    contact.FailureReason = null;
                    contact.ExternalMessageId = response.Data?.Id;
                    contact.QueuedAtUtc = DateTime.UtcNow;
                    queued++;
                }
                else
                {
                    contact.Status = "FAILED";
                    contact.FailureReason = response.Error?.Details ?? response.Message ?? "Failed to queue campaign message.";
                    failed++;
                }
            }
            catch (Exception ex)
            {
                contact.Status = "FAILED";
                contact.FailureReason = ex.Message;
                failed++;
            }
        }

        campaign.SentCount = queued;
        campaign.FailedCount = failed;
        campaign.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Campaign {CampaignId} launched. Queued={QueuedCount}, Failed={FailedCount}, Total={TotalCount}",
            campaignId,
            queued,
            failed,
            campaign.TotalContacts);

        if (queued == 0)
        {
            return ApiResponse<bool>.Fail("No campaign messages were queued.", HttpStatusCode.BadRequest);
        }

        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<PagedResult<CampaignContact>>> GetCampaignContactsAsync(int companyId, long campaignId, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var exists = await _db.Campaigns.AnyAsync(c => c.CompanyId == companyId && c.CampaignId == campaignId, ct);
        if (!exists)
        {
            return ApiResponse<PagedResult<CampaignContact>>.Fail("Campaign not found", HttpStatusCode.NotFound);
        }

        var q = _db.CampaignContacts.Where(cc => cc.CampaignId == campaignId);
        var total = await q.CountAsync(ct);
        var items = await q
            .OrderBy(cc => cc.CampaignContactId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return ApiResponse<PagedResult<CampaignContact>>.Ok(new PagedResult<CampaignContact>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }

    private async Task<CampaignRecipientValidationResultDto> ValidateRecipientsCoreAsync(
        int companyId,
        CampaignRecipientValidationRequest request,
        CancellationToken ct)
    {
        var validByPhone = new Dictionary<string, CampaignRecipientValidationItemDto>(StringComparer.Ordinal);
        var invalidItems = new List<CampaignRecipientValidationItemDto>();

        foreach (var rawPhone in request.PhoneNumbers ?? [])
        {
            var trimmed = rawPhone?.Trim() ?? string.Empty;
            var normalized = PhoneNumberNormalizer.Normalize(trimmed);

            if (normalized is null)
            {
                invalidItems.Add(new CampaignRecipientValidationItemDto
                {
                    PhoneNumber = trimmed,
                    IsWhatsAppAccountConfirmed = false,
                    Status = "INVALID",
                    Error = "Invalid phone number format."
                });
                continue;
            }

            AddOrMergeRecipient(validByPhone, normalized, null);
        }

        var requestedContactIds = (request.ContactIds ?? [])
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (requestedContactIds.Count > 0)
        {
            var contacts = await _db.Contacts
                .AsNoTracking()
                .Where(c => c.CompanyId == companyId && requestedContactIds.Contains(c.ContactId))
                .Select(c => new { c.ContactId, c.PhoneNumber })
                .ToListAsync(ct);

            var foundContactIds = contacts.Select(c => c.ContactId).ToHashSet();
            foreach (var contact in contacts)
            {
                var normalized = PhoneNumberNormalizer.Normalize(contact.PhoneNumber);
                if (normalized is null)
                {
                    invalidItems.Add(new CampaignRecipientValidationItemDto
                    {
                        PhoneNumber = contact.PhoneNumber,
                        ContactId = contact.ContactId,
                        IsWhatsAppAccountConfirmed = false,
                        Status = "INVALID",
                        Error = "Contact phone number format is invalid."
                    });
                    continue;
                }

                AddOrMergeRecipient(validByPhone, normalized, contact.ContactId);
            }

            foreach (var missingContactId in requestedContactIds.Where(id => !foundContactIds.Contains(id)))
            {
                invalidItems.Add(new CampaignRecipientValidationItemDto
                {
                    PhoneNumber = string.Empty,
                    ContactId = missingContactId,
                    IsWhatsAppAccountConfirmed = false,
                    Status = "INVALID",
                    Error = $"Contact with id {missingContactId} was not found."
                });
            }
        }

        var validRecipients = validByPhone.Values.ToList();
        if (validRecipients.Count > 0)
        {
            var config = await ResolveCampaignConfigAsync(companyId, request.WhatsAppPhoneNumberId, ct);

            foreach (var batch in validRecipients.Chunk(ContactLookupBatchSize))
            {
                var lookup = await CheckWhatsAppContactsBatchAsync(config, batch.Select(x => x.PhoneNumber).ToList(), ct);
                foreach (var recipient in batch)
                {
                    if (!lookup.TryGetValue(recipient.PhoneNumber, out var state))
                    {
                        recipient.IsWhatsAppAccountConfirmed = false;
                        recipient.Status = "ERROR";
                        recipient.Error = "Recipient verification did not return a result.";
                        continue;
                    }

                    recipient.IsWhatsAppAccountConfirmed = state.IsConfirmed;
                    recipient.Status = state.Status;
                    recipient.WaId = state.WaId;
                    recipient.Error = state.Error;
                }
            }
        }

        var recipients = new List<CampaignRecipientValidationItemDto>(validRecipients.Count + invalidItems.Count);
        recipients.AddRange(validRecipients);
        recipients.AddRange(invalidItems);

        return new CampaignRecipientValidationResultDto
        {
            TotalRecipients = recipients.Count,
            ConfirmedRecipients = recipients.Count(x => x.IsWhatsAppAccountConfirmed),
            NotWhatsAppRecipients = recipients.Count(x => x.Status == "NOT_WHATSAPP"),
            InvalidRecipients = recipients.Count(x => x.Status == "INVALID"),
            ErrorRecipients = recipients.Count(x => x.Status == "ERROR"),
            Recipients = recipients
        };
    }

    private async Task<TenantWhatsAppConfig> ResolveCampaignConfigAsync(int companyId, int? whatsAppPhoneNumberId, CancellationToken ct)
    {
        if (whatsAppPhoneNumberId.HasValue)
        {
            var config = await _tenantWhatsAppConfigService.GetConfigByWhatsAppPhoneNumberIdAsync(whatsAppPhoneNumberId.Value, ct);
            if (config is null || config.CompanyId != companyId)
            {
                throw new InvalidOperationException("Selected WhatsApp phone number is invalid for this company.");
            }

            return config;
        }

        return await _tenantWhatsAppConfigService.GetRequiredConfigAsync(companyId, ct);
    }

    private async Task<Dictionary<string, RecipientLookupResult>> CheckWhatsAppContactsBatchAsync(
        TenantWhatsAppConfig config,
        IReadOnlyCollection<string> phoneNumbers,
        CancellationToken ct)
    {
        var result = new Dictionary<string, RecipientLookupResult>(StringComparer.Ordinal);
        if (phoneNumbers.Count == 0)
        {
            return result;
        }

        var payload = new
        {
            messaging_product = "whatsapp",
            blocking = "wait",
            force_check = true,
            contacts = phoneNumbers
        };

        var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        var response = await _whatsAppGraphClient.SendAsync(config, HttpMethod.Post, $"{config.PhoneNumberId}/contacts", content, ct);

        if (!response.Success || response.Data is null)
        {
            var error = response.Error?.Details ?? response.Message ?? "Recipient verification failed.";
            foreach (var phoneNumber in phoneNumbers)
            {
                result[phoneNumber] = new RecipientLookupResult(false, "ERROR", null, error);
            }

            return result;
        }

        var contacts = response.Data.Contacts ?? [];
        foreach (var contact in contacts)
        {
            if (contact is not JsonElement element || element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var input = element.TryGetProperty("input", out var inputNode) ? inputNode.GetString() : null;
            var normalized = PhoneNumberNormalizer.Normalize(input);
            if (normalized is null)
            {
                continue;
            }

            var status = element.TryGetProperty("status", out var statusNode)
                ? statusNode.GetString()
                : null;
            var waId = element.TryGetProperty("wa_id", out var waIdNode)
                ? waIdNode.GetString()
                : null;

            var isConfirmed = string.Equals(status, "valid", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(waId);
            var normalizedStatus = isConfirmed ? "CONFIRMED" : "NOT_WHATSAPP";
            var error = isConfirmed
                ? null
                : ExtractLookupError(element) ?? "Phone number does not have an active WhatsApp account.";

            result[normalized] = new RecipientLookupResult(isConfirmed, normalizedStatus, waId, error);
        }

        foreach (var phoneNumber in phoneNumbers)
        {
            if (!result.ContainsKey(phoneNumber))
            {
                result[phoneNumber] = new RecipientLookupResult(
                    false,
                    "ERROR",
                    null,
                    "Recipient verification did not return a result.");
            }
        }

        return result;
    }

    private static string? ExtractLookupError(JsonElement lookupNode)
    {
        if (!lookupNode.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Array || errors.GetArrayLength() == 0)
        {
            return null;
        }

        var first = errors[0];
        if (first.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var message = first.TryGetProperty("message", out var messageNode) ? messageNode.GetString() : null;
        var code = first.TryGetProperty("code", out var codeNode) ? codeNode.GetRawText() : null;
        if (string.IsNullOrWhiteSpace(code))
        {
            return message;
        }

        return string.IsNullOrWhiteSpace(message) ? $"Meta error code: {code}" : $"{message} (code: {code})";
    }

    private static void AddOrMergeRecipient(
        IDictionary<string, CampaignRecipientValidationItemDto> recipientsByPhone,
        string normalizedPhone,
        long? contactId)
    {
        if (!recipientsByPhone.TryGetValue(normalizedPhone, out var existing))
        {
            recipientsByPhone[normalizedPhone] = new CampaignRecipientValidationItemDto
            {
                PhoneNumber = normalizedPhone,
                ContactId = contactId,
                IsWhatsAppAccountConfirmed = false,
                Status = "UNVERIFIED"
            };
            return;
        }

        if (!existing.ContactId.HasValue && contactId.HasValue)
        {
            existing.ContactId = contactId.Value;
        }
    }

    private static List<TemplateComponentDto> ParseTemplateComponents(string? templateParametersJson)
    {
        if (string.IsNullOrWhiteSpace(templateParametersJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(templateParametersJson);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                return JsonSerializer.Deserialize<List<TemplateComponentDto>>(root.GetRawText(), JsonOptions) ?? [];
            }

            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("components", out var components) && components.ValueKind == JsonValueKind.Array)
            {
                return JsonSerializer.Deserialize<List<TemplateComponentDto>>(components.GetRawText(), JsonOptions) ?? [];
            }

            return [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed record RecipientLookupResult(bool IsConfirmed, string Status, string? WaId, string? Error);
}
