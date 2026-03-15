using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Infrastructure.Data;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/phone-numbers")]
[Authorize]
public sealed class PhoneNumbersController : ApiControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IMetaVerificationService _metaVerificationService;
    private readonly ISubscriptionValidationService _subscriptionValidationService;

    public PhoneNumbersController(
        ApplicationDbContext dbContext,
        ITenantContextAccessor tenantContextAccessor,
        IMetaVerificationService metaVerificationService,
        ISubscriptionValidationService subscriptionValidationService)
    {
        _dbContext = dbContext;
        _tenantContextAccessor = tenantContextAccessor;
        _metaVerificationService = metaVerificationService;
        _subscriptionValidationService = subscriptionValidationService;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var result = await _dbContext.WhatsAppPhoneNumbers
            .AsNoTracking()
            .Where(x => x.CompanyId == tenant.CompanyId)
            .ToListAsync(cancellationToken);

        return ToActionResult(ApiResponse<List<WhatsAppPhoneNumber>>.Ok(result));
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetById([FromRoute] int id, CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var record = await _dbContext.WhatsAppPhoneNumbers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.WhatsAppPhoneNumberId == id && x.CompanyId == tenant.CompanyId, cancellationToken);

        if (record is null)
        {
            return ToActionResult(ApiResponse<object>.Fail("Phone number not found.", System.Net.HttpStatusCode.NotFound));
        }

        return ToActionResult(ApiResponse<WhatsAppPhoneNumber>.Ok(record));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] WhatsAppPhoneNumberUpsertRequest request, CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        try
        {
            await _subscriptionValidationService.ValidatePhoneNumberLimitAsync(tenant.CompanyId, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return ToActionResult(ApiResponse<object>.Fail(ex.Message, System.Net.HttpStatusCode.BadRequest));
        }

        var resolvedAccountId = await ResolveWhatsAppAccountIdAsync(tenant.CompanyId, request, cancellationToken);
        if (string.IsNullOrWhiteSpace(resolvedAccountId))
        {
            return ToActionResult(ApiResponse<object>.Fail(
                "WhatsApp account not found for this company. Use local WhatsAppAccountId or provide BusinessAccountId.",
                System.Net.HttpStatusCode.BadRequest));
        }

        if (request.IsDefault)
        {
            await ClearDefaultPhoneNumbersAsync(tenant.CompanyId, cancellationToken);
        }

        var entity = new WhatsAppPhoneNumber
        {
            CompanyId = tenant.CompanyId,
            WhatsAppAccountId = resolvedAccountId,
            PhoneNumberId = request.PhoneNumberId.Trim(),
            DisplayPhoneNumber = request.DisplayPhoneNumber.Trim(),
            VerifiedName = request.VerifiedName,
            IsDefault = request.IsDefault,
            IsActive = request.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.WhatsAppPhoneNumbers.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToActionResult(ApiResponse<WhatsAppPhoneNumber>.Ok(entity, "Phone number created."));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update([FromRoute] int id, [FromBody] WhatsAppPhoneNumberUpsertRequest request, CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var entity = await _dbContext.WhatsAppPhoneNumbers
            .FirstOrDefaultAsync(x => x.WhatsAppPhoneNumberId == id && x.CompanyId == tenant.CompanyId, cancellationToken);

        if (entity is null)
        {
            return ToActionResult(ApiResponse<object>.Fail("Phone number not found.", System.Net.HttpStatusCode.NotFound));
        }

        var resolvedAccountId = await ResolveWhatsAppAccountIdAsync(tenant.CompanyId, request, cancellationToken);
        if (string.IsNullOrWhiteSpace(resolvedAccountId))
        {
            return ToActionResult(ApiResponse<object>.Fail(
                "WhatsApp account not found for this company. Use local WhatsAppAccountId or provide BusinessAccountId.",
                System.Net.HttpStatusCode.BadRequest));
        }

        if (request.IsActive && !entity.IsActive)
        {
            try
            {
                await _subscriptionValidationService.ValidatePhoneNumberLimitAsync(tenant.CompanyId, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                return ToActionResult(ApiResponse<object>.Fail(ex.Message, System.Net.HttpStatusCode.BadRequest));
            }
        }

        if (request.IsDefault)
        {
            await ClearDefaultPhoneNumbersAsync(tenant.CompanyId, cancellationToken);
        }

        entity.WhatsAppAccountId = resolvedAccountId;
        entity.PhoneNumberId = request.PhoneNumberId.Trim();
        entity.DisplayPhoneNumber = request.DisplayPhoneNumber.Trim();
        entity.VerifiedName = request.VerifiedName;
        entity.IsDefault = request.IsDefault;
        entity.IsActive = request.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToActionResult(ApiResponse<WhatsAppPhoneNumber>.Ok(entity, "Phone number updated."));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var entity = await _dbContext.WhatsAppPhoneNumbers
            .FirstOrDefaultAsync(x => x.WhatsAppPhoneNumberId == id && x.CompanyId == tenant.CompanyId, cancellationToken);

        if (entity is null)
        {
            return ToActionResult(ApiResponse<object>.Fail("Phone number not found.", System.Net.HttpStatusCode.NotFound));
        }

        _dbContext.WhatsAppPhoneNumbers.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToActionResult(ApiResponse<object>.Ok(new { id }, "Phone number deleted."));
    }

    /// <summary>
    /// Sync phone numbers from an external source (e.g., after Meta API retrieval).
    /// Creates new numbers or updates existing ones. No duplicates.
    /// </summary>
    [HttpPost("sync")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Sync([FromBody] PhoneNumberSyncRequest request, CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var result = await _metaVerificationService.SyncPhoneNumbersAsync(tenant.CompanyId, request, cancellationToken);
        return ToActionResult(ApiResponse<PhoneNumberSyncResponse>.Ok(result, $"Synced {result.Total} phone numbers ({result.Created} created, {result.Updated} updated, {result.Skipped} skipped)."));
    }

    private async Task ClearDefaultPhoneNumbersAsync(int companyId, CancellationToken cancellationToken)
    {
        var defaults = await _dbContext.WhatsAppPhoneNumbers
            .Where(x => x.CompanyId == companyId && x.IsDefault)
            .ToListAsync(cancellationToken);
        foreach (var item in defaults)
        {
            item.IsDefault = false;
            item.UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    private async Task<string?> ResolveWhatsAppAccountIdAsync(int companyId, WhatsAppPhoneNumberUpsertRequest request, CancellationToken cancellationToken)
    {
        // Try by WhatsAppAccountId first
        if (!string.IsNullOrWhiteSpace(request.WhatsAppAccountId))
        {
            var account = await _dbContext.WhatsAppAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.WhatsAppAccountId == request.WhatsAppAccountId.Trim()
                    && x.CompanyId == companyId, cancellationToken);
            if (account is not null)
            {
                return account.WhatsAppAccountId;
            }
        }

        // Try by BusinessAccountId
        if (!string.IsNullOrWhiteSpace(request.BusinessAccountId))
        {
            var account = await _dbContext.WhatsAppAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.BusinessAccountId == request.BusinessAccountId.Trim()
                    && x.CompanyId == companyId, cancellationToken);
            if (account is not null)
            {
                return account.WhatsAppAccountId;
            }

            // Try via MetaBusinessAccounts
            var metaAccount = await _dbContext.MetaBusinessAccounts
                .AsNoTracking()
                .Where(x => x.BusinessId == request.BusinessAccountId.Trim() && x.CompanyId == companyId)
                .Join(
                    _dbContext.WhatsAppAccounts.AsNoTracking().Where(a => a.CompanyId == companyId),
                    m => m.MetaBusinessAccountId,
                    a => a.MetaBusinessAccountId,
                    (m, a) => a.WhatsAppAccountId)
                .FirstOrDefaultAsync(cancellationToken);

            if (metaAccount is not null)
            {
                return metaAccount;
            }
        }

        return null;
    }
}
