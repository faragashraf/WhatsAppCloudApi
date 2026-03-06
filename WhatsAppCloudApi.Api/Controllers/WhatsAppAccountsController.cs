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
[Route("api/whatsapp-accounts")]
[Authorize]
public sealed class WhatsAppAccountsController : ApiControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly ISubscriptionValidationService _subscriptionValidationService;

    public WhatsAppAccountsController(
        ApplicationDbContext dbContext,
        ITenantContextAccessor tenantContextAccessor,
        ISubscriptionValidationService subscriptionValidationService)
    {
        _dbContext = dbContext;
        _tenantContextAccessor = tenantContextAccessor;
        _subscriptionValidationService = subscriptionValidationService;
    }

    [HttpGet("meta-business-accounts")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetMetaBusinessAccounts(CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var records = await _dbContext.MetaBusinessAccounts
            .AsNoTracking()
            .Where(x => x.CompanyId == tenant.CompanyId)
            .ToListAsync(cancellationToken);

        return ToActionResult(ApiResponse<List<MetaBusinessAccount>>.Ok(records));
    }

    [HttpPost("meta-business-accounts")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateMetaBusinessAccount([FromBody] MetaBusinessAccountUpsertRequest request, CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var entity = new MetaBusinessAccount
        {
            CompanyId = tenant.CompanyId,
            BusinessId = request.BusinessId.Trim(),
            Name = request.Name.Trim(),
            AccessToken = request.AccessToken,
            IsActive = request.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.MetaBusinessAccounts.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToActionResult(ApiResponse<MetaBusinessAccount>.Ok(entity, "Meta business account created."));
    }

    [HttpPut("meta-business-accounts/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateMetaBusinessAccount([FromRoute] int id, [FromBody] MetaBusinessAccountUpsertRequest request, CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var entity = await _dbContext.MetaBusinessAccounts
            .FirstOrDefaultAsync(x => x.MetaBusinessAccountId == id && x.CompanyId == tenant.CompanyId, cancellationToken);

        if (entity is null)
        {
            return ToActionResult(ApiResponse<object>.Fail("Meta business account not found.", System.Net.HttpStatusCode.NotFound));
        }

        entity.BusinessId = request.BusinessId.Trim();
        entity.Name = request.Name.Trim();
        entity.AccessToken = request.AccessToken;
        entity.IsActive = request.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToActionResult(ApiResponse<MetaBusinessAccount>.Ok(entity, "Meta business account updated."));
    }

    [HttpDelete("meta-business-accounts/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteMetaBusinessAccount([FromRoute] int id, CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var entity = await _dbContext.MetaBusinessAccounts
            .FirstOrDefaultAsync(x => x.MetaBusinessAccountId == id && x.CompanyId == tenant.CompanyId, cancellationToken);

        if (entity is null)
        {
            return ToActionResult(ApiResponse<object>.Fail("Meta business account not found.", System.Net.HttpStatusCode.NotFound));
        }

        _dbContext.MetaBusinessAccounts.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToActionResult(ApiResponse<object>.Ok(new { id }, "Meta business account deleted."));
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetWhatsAppAccounts(CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var records = await _dbContext.WhatsAppAccounts
            .AsNoTracking()
            .Where(x => x.CompanyId == tenant.CompanyId)
            .ToListAsync(cancellationToken);

        return ToActionResult(ApiResponse<List<WhatsAppAccount>>.Ok(records));
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetWhatsAppAccountById([FromRoute] int id, CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var record = await _dbContext.WhatsAppAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.WhatsAppAccountId == id && x.CompanyId == tenant.CompanyId, cancellationToken);

        if (record is null)
        {
            return ToActionResult(ApiResponse<object>.Fail("WhatsApp account not found.", System.Net.HttpStatusCode.NotFound));
        }

        return ToActionResult(ApiResponse<WhatsAppAccount>.Ok(record));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateWhatsAppAccount([FromBody] WhatsAppAccountUpsertRequest request, CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        await _subscriptionValidationService.ValidateWhatsAppAccountLimitAsync(tenant.CompanyId, cancellationToken);

        if (request.MetaBusinessAccountId.HasValue)
        {
            var metaExists = await _dbContext.MetaBusinessAccounts
                .AnyAsync(x => x.MetaBusinessAccountId == request.MetaBusinessAccountId && x.CompanyId == tenant.CompanyId, cancellationToken);
            if (!metaExists)
            {
                return ToActionResult(ApiResponse<object>.Fail("Meta business account not found for this company.", System.Net.HttpStatusCode.BadRequest));
            }
        }

        if (request.IsDefault)
        {
            await ClearDefaultAccountsAsync(tenant.CompanyId, cancellationToken);
        }

        var entity = new WhatsAppAccount
        {
            CompanyId = tenant.CompanyId,
            MetaBusinessAccountId = request.MetaBusinessAccountId,
            BusinessAccountId = request.BusinessAccountId.Trim(),
            Name = request.Name.Trim(),
            AccessToken = request.AccessToken,
            VerifyToken = request.VerifyToken,
            AppSecret = request.AppSecret,
            IsDefault = request.IsDefault,
            IsActive = request.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.WhatsAppAccounts.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToActionResult(ApiResponse<WhatsAppAccount>.Ok(entity, "WhatsApp account created."));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateWhatsAppAccount([FromRoute] int id, [FromBody] WhatsAppAccountUpsertRequest request, CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var entity = await _dbContext.WhatsAppAccounts
            .FirstOrDefaultAsync(x => x.WhatsAppAccountId == id && x.CompanyId == tenant.CompanyId, cancellationToken);

        if (entity is null)
        {
            return ToActionResult(ApiResponse<object>.Fail("WhatsApp account not found.", System.Net.HttpStatusCode.NotFound));
        }

        if (request.MetaBusinessAccountId.HasValue)
        {
            var metaExists = await _dbContext.MetaBusinessAccounts
                .AnyAsync(x => x.MetaBusinessAccountId == request.MetaBusinessAccountId && x.CompanyId == tenant.CompanyId, cancellationToken);
            if (!metaExists)
            {
                return ToActionResult(ApiResponse<object>.Fail("Meta business account not found for this company.", System.Net.HttpStatusCode.BadRequest));
            }
        }

        if (request.IsActive && !entity.IsActive)
        {
            await _subscriptionValidationService.ValidateWhatsAppAccountLimitAsync(tenant.CompanyId, cancellationToken);
        }

        if (request.IsDefault)
        {
            await ClearDefaultAccountsAsync(tenant.CompanyId, cancellationToken);
        }

        entity.MetaBusinessAccountId = request.MetaBusinessAccountId;
        entity.BusinessAccountId = request.BusinessAccountId.Trim();
        entity.Name = request.Name.Trim();
        entity.AccessToken = request.AccessToken;
        entity.VerifyToken = request.VerifyToken;
        entity.AppSecret = request.AppSecret;
        entity.IsDefault = request.IsDefault;
        entity.IsActive = request.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToActionResult(ApiResponse<WhatsAppAccount>.Ok(entity, "WhatsApp account updated."));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteWhatsAppAccount([FromRoute] int id, CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var entity = await _dbContext.WhatsAppAccounts
            .FirstOrDefaultAsync(x => x.WhatsAppAccountId == id && x.CompanyId == tenant.CompanyId, cancellationToken);

        if (entity is null)
        {
            return ToActionResult(ApiResponse<object>.Fail("WhatsApp account not found.", System.Net.HttpStatusCode.NotFound));
        }

        _dbContext.WhatsAppAccounts.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToActionResult(ApiResponse<object>.Ok(new { id }, "WhatsApp account deleted."));
    }

    private async Task ClearDefaultAccountsAsync(int companyId, CancellationToken cancellationToken)
    {
        var defaults = await _dbContext.WhatsAppAccounts
            .Where(x => x.CompanyId == companyId && x.IsDefault)
            .ToListAsync(cancellationToken);
        foreach (var item in defaults)
        {
            item.IsDefault = false;
            item.UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}
