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
[Route("api/subscriptions")]
[Authorize]
public sealed class SubscriptionsController : ApiControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly ISubscriptionValidationService _subscriptionValidationService;

    public SubscriptionsController(
        ApplicationDbContext dbContext,
        ITenantContextAccessor tenantContextAccessor,
        ISubscriptionValidationService subscriptionValidationService)
    {
        _dbContext = dbContext;
        _tenantContextAccessor = tenantContextAccessor;
        _subscriptionValidationService = subscriptionValidationService;
    }

    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans(CancellationToken cancellationToken)
    {
        var plans = await _dbContext.SubscriptionPlans
            .AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);

        return ToActionResult(ApiResponse<List<SubscriptionPlan>>.Ok(plans));
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetCompanySubscriptions(CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var subscriptions = await _dbContext.CompanySubscriptions
            .AsNoTracking()
            .Where(x => x.CompanyId == tenant.CompanyId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return ToActionResult(ApiResponse<List<CompanySubscription>>.Ok(subscriptions));
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var now = DateTime.UtcNow;

        var current = await _dbContext.CompanySubscriptions
            .AsNoTracking()
            .Where(x => x.CompanyId == tenant.CompanyId && x.IsActive)
            .Where(x =>
                (x.Status == "ACTIVE" && (x.EndDate == null || x.EndDate >= now)) ||
                (x.Status == "TRIAL" && x.TrialEndDate != null && x.TrialEndDate >= now))
            .OrderByDescending(x => x.EndDate ?? x.TrialEndDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (current is null)
        {
            return ToActionResult(ApiResponse<object>.Fail("Subscription expired.", System.Net.HttpStatusCode.BadRequest));
        }

        return ToActionResult(ApiResponse<CompanySubscription>.Ok(current));
    }

    [HttpGet("usage")]
    public async Task<IActionResult> GetUsage(CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var usage = await _subscriptionValidationService.GetUsageSnapshotAsync(tenant.CompanyId, cancellationToken);
        return ToActionResult(ApiResponse<SubscriptionUsageSnapshotDto>.Ok(usage));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CompanySubscriptionUpsertRequest request, CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var planExists = await _dbContext.SubscriptionPlans
            .AnyAsync(x => x.SubscriptionPlanId == request.SubscriptionPlanId && x.IsActive, cancellationToken);

        if (!planExists)
        {
            return ToActionResult(ApiResponse<object>.Fail("Subscription plan not found.", System.Net.HttpStatusCode.BadRequest));
        }

        var entity = new CompanySubscription
        {
            CompanyId = tenant.CompanyId,
            SubscriptionPlanId = request.SubscriptionPlanId,
            Status = request.Status,
            TrialStartDate = request.TrialStartDate,
            TrialEndDate = request.TrialEndDate,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = request.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.CompanySubscriptions.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToActionResult(ApiResponse<CompanySubscription>.Ok(entity, "Subscription created."));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update([FromRoute] int id, [FromBody] CompanySubscriptionUpsertRequest request, CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var planExists = await _dbContext.SubscriptionPlans
            .AnyAsync(x => x.SubscriptionPlanId == request.SubscriptionPlanId && x.IsActive, cancellationToken);

        if (!planExists)
        {
            return ToActionResult(ApiResponse<object>.Fail("Subscription plan not found.", System.Net.HttpStatusCode.BadRequest));
        }

        var entity = await _dbContext.CompanySubscriptions
            .FirstOrDefaultAsync(x => x.CompanySubscriptionId == id && x.CompanyId == tenant.CompanyId, cancellationToken);

        if (entity is null)
        {
            return ToActionResult(ApiResponse<object>.Fail("Subscription not found.", System.Net.HttpStatusCode.NotFound));
        }

        entity.SubscriptionPlanId = request.SubscriptionPlanId;
        entity.Status = request.Status;
        entity.TrialStartDate = request.TrialStartDate;
        entity.TrialEndDate = request.TrialEndDate;
        entity.StartDate = request.StartDate;
        entity.EndDate = request.EndDate;
        entity.IsActive = request.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToActionResult(ApiResponse<CompanySubscription>.Ok(entity, "Subscription updated."));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var entity = await _dbContext.CompanySubscriptions
            .FirstOrDefaultAsync(x => x.CompanySubscriptionId == id && x.CompanyId == tenant.CompanyId, cancellationToken);

        if (entity is null)
        {
            return ToActionResult(ApiResponse<object>.Fail("Subscription not found.", System.Net.HttpStatusCode.NotFound));
        }

        _dbContext.CompanySubscriptions.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToActionResult(ApiResponse<object>.Ok(new { id }, "Subscription deleted."));
    }
}
