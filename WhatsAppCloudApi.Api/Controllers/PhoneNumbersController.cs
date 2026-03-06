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

    public PhoneNumbersController(ApplicationDbContext dbContext, ITenantContextAccessor tenantContextAccessor)
    {
        _dbContext = dbContext;
        _tenantContextAccessor = tenantContextAccessor;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var records = await _dbContext.WhatsAppPhoneNumbers
            .AsNoTracking()
            .Where(x => x.CompanyId == tenant.CompanyId)
            .ToListAsync(cancellationToken);

        return ToActionResult(ApiResponse<List<WhatsAppPhoneNumber>>.Ok(records));
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
        var accountExists = await _dbContext.WhatsAppAccounts
            .AnyAsync(
                x => x.WhatsAppAccountId == request.WhatsAppAccountId && x.CompanyId == tenant.CompanyId,
                cancellationToken);
        if (!accountExists)
        {
            return ToActionResult(ApiResponse<object>.Fail("WhatsApp account not found for this company.", System.Net.HttpStatusCode.BadRequest));
        }

        if (request.IsDefault)
        {
            await ClearDefaultPhoneNumbersAsync(tenant.CompanyId, cancellationToken);
        }

        var entity = new WhatsAppPhoneNumber
        {
            CompanyId = tenant.CompanyId,
            WhatsAppAccountId = request.WhatsAppAccountId,
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

        var accountExists = await _dbContext.WhatsAppAccounts
            .AnyAsync(
                x => x.WhatsAppAccountId == request.WhatsAppAccountId && x.CompanyId == tenant.CompanyId,
                cancellationToken);
        if (!accountExists)
        {
            return ToActionResult(ApiResponse<object>.Fail("WhatsApp account not found for this company.", System.Net.HttpStatusCode.BadRequest));
        }

        if (request.IsDefault)
        {
            await ClearDefaultPhoneNumbersAsync(tenant.CompanyId, cancellationToken);
        }

        entity.WhatsAppAccountId = request.WhatsAppAccountId;
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
}
