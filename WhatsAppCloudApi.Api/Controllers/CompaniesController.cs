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
[Route("api/companies")]
[Authorize]
public sealed class CompaniesController : ApiControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public CompaniesController(ApplicationDbContext dbContext, ITenantContextAccessor tenantContextAccessor)
    {
        _dbContext = dbContext;
        _tenantContextAccessor = tenantContextAccessor;
    }

    [HttpGet]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var company = await _dbContext.Companies
            .AsNoTracking()
            .Where(x => x.CompanyId == tenant.CompanyId)
            .ToListAsync(cancellationToken);

        return ToActionResult(ApiResponse<List<Company>>.Ok(company));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById([FromRoute] int id, CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        if (id != tenant.CompanyId)
        {
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        }

        var company = await _dbContext.Companies.AsNoTracking().FirstOrDefaultAsync(x => x.CompanyId == id, cancellationToken);
        if (company is null)
        {
            return ToActionResult(ApiResponse<object>.Fail("Company not found.", System.Net.HttpStatusCode.NotFound));
        }

        return ToActionResult(ApiResponse<Company>.Ok(company));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CompanyUpsertRequest request, CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var existing = await _dbContext.Companies.AnyAsync(x => x.CompanyId == tenant.CompanyId, cancellationToken);
        if (existing)
        {
            return ToActionResult(ApiResponse<object>.Fail("Company already exists. Use register-company for new tenants.", System.Net.HttpStatusCode.BadRequest));
        }

        var company = new Company
        {
            CompanyId = tenant.CompanyId,
            CompanyName = ResolveCompanyName(request),
            Email = ResolveEmail(request),
            Phone = request.Phone,
            Status = ResolveStatus(request),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Companies.Add(company);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToActionResult(ApiResponse<Company>.Ok(company, "Company created."));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update([FromRoute] int id, [FromBody] CompanyUpsertRequest request, CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        if (id != tenant.CompanyId)
        {
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        }

        var company = await _dbContext.Companies.FirstOrDefaultAsync(x => x.CompanyId == id, cancellationToken);
        if (company is null)
        {
            return ToActionResult(ApiResponse<object>.Fail("Company not found.", System.Net.HttpStatusCode.NotFound));
        }

        company.CompanyName = ResolveCompanyName(request);
        company.Email = ResolveEmail(request);
        company.Phone = request.Phone;
        company.Status = ResolveStatus(request);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToActionResult(ApiResponse<Company>.Ok(company, "Company updated."));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        if (id != tenant.CompanyId)
        {
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        }

        var company = await _dbContext.Companies.FirstOrDefaultAsync(x => x.CompanyId == id, cancellationToken);
        if (company is null)
        {
            return ToActionResult(ApiResponse<object>.Fail("Company not found.", System.Net.HttpStatusCode.NotFound));
        }

        company.Status = "INACTIVE";
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToActionResult(ApiResponse<object>.Ok(new { company.CompanyId }, "Company deactivated."));
    }

    private static string ResolveCompanyName(CompanyUpsertRequest request)
    {
        var value = string.IsNullOrWhiteSpace(request.CompanyName) ? request.Name : request.CompanyName;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Company name is required.");
        }

        return value.Trim();
    }

    private static string ResolveEmail(CompanyUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new InvalidOperationException("Company email is required.");
        }

        return request.Email.Trim().ToLowerInvariant();
    }

    private static string ResolveStatus(CompanyUpsertRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            return request.Status.Trim().ToUpperInvariant();
        }

        if (request.IsActive.HasValue)
        {
            return request.IsActive.Value ? "ACTIVE" : "INACTIVE";
        }

        return "ACTIVE";
    }
}
