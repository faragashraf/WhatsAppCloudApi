using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Infrastructure.Data;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/automation")]
[Authorize]
public sealed class AutomationController : ApiControllerBase
{
    private readonly IAutomationService _automationService;
    private readonly ITenantContextAccessor _tenantContext;
    private readonly ApplicationDbContext _db;

    public AutomationController(IAutomationService automationService, ITenantContextAccessor tenantContext, ApplicationDbContext db)
    {
        _automationService = automationService;
        _tenantContext = tenantContext;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetRules(CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.AutomationView)
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        return ToActionResult(await _automationService.GetRulesAsync(ctx.CompanyId, ct));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetRule(long id, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.AutomationView)
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        return ToActionResult(await _automationService.GetRuleByIdAsync(ctx.CompanyId, id, ct));
    }

    [HttpPost]
    public async Task<IActionResult> CreateRule([FromBody] AutomationRuleUpsertRequest request, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.AutomationCreate)
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        return ToActionResult(await _automationService.CreateRuleAsync(ctx.CompanyId, request, ct));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateRule(long id, [FromBody] AutomationRuleUpsertRequest request, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.AutomationEdit)
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        return ToActionResult(await _automationService.UpdateRuleAsync(ctx.CompanyId, id, request, ct));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteRule(long id, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.AutomationDelete)
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        return ToActionResult(await _automationService.DeleteRuleAsync(ctx.CompanyId, id, ct));
    }

    [HttpPost("{id:long}/toggle")]
    public async Task<IActionResult> ToggleRule(long id, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.AutomationEdit)
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        return ToActionResult(await _automationService.ToggleRuleAsync(ctx.CompanyId, id, ct));
    }

    private async Task<Domain.Models.UserPermissions> GetPermissions(int companyId, int userId, string role, CancellationToken ct)
    {
        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            return Domain.Models.UserPermissions.FullAccess();
        var user = await _db.CompanyUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.CompanyUserId == userId && u.CompanyId == companyId && u.IsActive, ct);
        return user?.EffectivePermissions ?? Domain.Models.UserPermissions.MemberDefault();
    }
}
