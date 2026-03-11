using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Infrastructure.Data;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Api.Controllers;

/// <summary>
/// Platform-level super admin endpoints for managing all companies / subscriptions.
/// Only users with IsSuperAdmin = true can access.
/// </summary>
[ApiController]
[Route("api/super-admin")]
[Authorize]
public sealed class SuperAdminController : ApiControllerBase
{
    private readonly ApplicationDbContext _db;

    public SuperAdminController(ApplicationDbContext db)
    {
        _db = db;
    }

    // ─── Helper ────────────────────────────────────────────────────
    private async Task<bool> IsSuperAdmin(CancellationToken ct)
    {
        var userIdClaim = User.FindFirst("UserId")?.Value;
        if (!int.TryParse(userIdClaim, out var userId)) return false;
        return await _db.CompanyUsers.AnyAsync(u => u.CompanyUserId == userId && u.IsSuperAdmin && u.IsActive, ct);
    }

    // ─── Companies ─────────────────────────────────────────────────

    [HttpGet("companies")]
    public async Task<IActionResult> GetCompanies([FromQuery] bool includeDeleted, CancellationToken ct)
    {
        if (!await IsSuperAdmin(ct))
            return Forbid();

        var query = _db.Companies.AsNoTracking().AsQueryable();
        if (!includeDeleted) query = query.Where(c => !c.IsDeleted);

        var companies = await query
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.CompanyId,
                c.CompanyName,
                c.Email,
                c.Phone,
                c.Status,
                c.IsDeleted,
                c.SuspendedAtUtc,
                c.DeletedAtUtc,
                c.CreatedAt,
                c.TrialStartDate,
                c.TrialEndDate,
                c.SubscriptionEndDate,
                UserCount = c.Users.Count(u => u.IsActive),
                ActiveSubscription = c.Subscriptions
                    .Where(s => s.IsActive)
                    .Select(s => new { s.CompanySubscriptionId, s.Status, s.SubscriptionPlan!.Name, s.TrialEndDate, s.EndDate })
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        return ToActionResult(ApiResponse<object>.Ok(companies));
    }

    [HttpGet("companies/{id:int}")]
    public async Task<IActionResult> GetCompany(int id, CancellationToken ct)
    {
        if (!await IsSuperAdmin(ct))
            return Forbid();

        var company = await _db.Companies
            .Include(c => c.Users.Where(u => u.IsActive))
            .Include(c => c.Subscriptions).ThenInclude(s => s.SubscriptionPlan)
            .FirstOrDefaultAsync(c => c.CompanyId == id, ct);

        if (company is null) return NotFound();

        return ToActionResult(ApiResponse<object>.Ok(new
        {
            company.CompanyId,
            company.CompanyName,
            company.Email,
            company.Phone,
            company.Status,
            company.IsDeleted,
            company.SuspendedAtUtc,
            company.DeletedAtUtc,
            company.CreatedAt,
            company.TrialStartDate,
            company.TrialEndDate,
            company.SubscriptionEndDate,
            Users = company.Users.Select(u => new { u.CompanyUserId, u.FullName, u.Email, u.Role, u.IsActive }),
            Subscriptions = company.Subscriptions.Select(s => new
            {
                s.CompanySubscriptionId,
                s.SubscriptionPlanId,
                PlanName = s.SubscriptionPlan?.Name,
                s.Status,
                s.TrialStartDate,
                s.TrialEndDate,
                s.StartDate,
                s.EndDate,
                s.IsActive,
            }),
        }));
    }

    public sealed record UpdateCompanyRequest(string CompanyName, string Email, string? Phone);

    [HttpPut("companies/{id:int}")]
    public async Task<IActionResult> UpdateCompany(int id, [FromBody] UpdateCompanyRequest request, CancellationToken ct)
    {
        if (!await IsSuperAdmin(ct))
            return Forbid();

        var company = await _db.Companies.FindAsync(new object[] { id }, ct);
        if (company is null) return NotFound();

        company.CompanyName = request.CompanyName;
        company.Email = request.Email;
        company.Phone = request.Phone;
        await _db.SaveChangesAsync(ct);

        return ToActionResult(ApiResponse<string>.Ok("Company updated."));
    }

    [HttpPost("companies/{id:int}/suspend")]
    public async Task<IActionResult> SuspendCompany(int id, CancellationToken ct)
    {
        if (!await IsSuperAdmin(ct))
            return Forbid();

        var company = await _db.Companies.FindAsync(new object[] { id }, ct);
        if (company is null) return NotFound();

        company.Status = "SUSPENDED";
        company.SuspendedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return ToActionResult(ApiResponse<string>.Ok("Company suspended."));
    }

    [HttpPost("companies/{id:int}/activate")]
    public async Task<IActionResult> ActivateCompany(int id, CancellationToken ct)
    {
        if (!await IsSuperAdmin(ct))
            return Forbid();

        var company = await _db.Companies.FindAsync(new object[] { id }, ct);
        if (company is null) return NotFound();

        company.Status = "ACTIVE";
        company.SuspendedAtUtc = null;
        company.IsDeleted = false;
        company.DeletedAtUtc = null;
        await _db.SaveChangesAsync(ct);

        return ToActionResult(ApiResponse<string>.Ok("Company activated."));
    }

    [HttpDelete("companies/{id:int}")]
    public async Task<IActionResult> SoftDeleteCompany(int id, CancellationToken ct)
    {
        if (!await IsSuperAdmin(ct))
            return Forbid();

        var company = await _db.Companies.FindAsync(new object[] { id }, ct);
        if (company is null) return NotFound();

        company.IsDeleted = true;
        company.DeletedAtUtc = DateTime.UtcNow;
        company.Status = "DELETED";
        await _db.SaveChangesAsync(ct);

        return ToActionResult(ApiResponse<string>.Ok("Company soft-deleted."));
    }

    // ─── Subscription management ──────────────────────────────────

    public sealed record UpsertSubscriptionRequest(int SubscriptionPlanId, string Status, DateTime? StartDate, DateTime? EndDate, bool IsActive);

    [HttpPost("companies/{companyId:int}/subscriptions")]
    public async Task<IActionResult> UpsertSubscription(int companyId, [FromBody] UpsertSubscriptionRequest request, CancellationToken ct)
    {
        if (!await IsSuperAdmin(ct))
            return Forbid();

        var company = await _db.Companies.FindAsync(new object[] { companyId }, ct);
        if (company is null) return NotFound();

        var plan = await _db.SubscriptionPlans.FindAsync(new object[] { request.SubscriptionPlanId }, ct);
        if (plan is null) return BadRequest("Invalid plan.");

        // Deactivate current
        var existing = await _db.CompanySubscriptions
            .Where(s => s.CompanyId == companyId && s.IsActive)
            .ToListAsync(ct);
        foreach (var s in existing) s.IsActive = false;

        var sub = new CompanySubscription
        {
            CompanyId = companyId,
            SubscriptionPlanId = request.SubscriptionPlanId,
            Status = request.Status,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = request.IsActive,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.CompanySubscriptions.Add(sub);

        company.SubscriptionEndDate = request.EndDate;
        await _db.SaveChangesAsync(ct);

        return ToActionResult(ApiResponse<string>.Ok("Subscription updated."));
    }

    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans(CancellationToken ct)
    {
        if (!await IsSuperAdmin(ct))
            return Forbid();

        var plans = await _db.SubscriptionPlans.AsNoTracking().OrderBy(p => p.MonthlyPrice).ToListAsync(ct);
        return ToActionResult(ApiResponse<object>.Ok(plans));
    }
}
