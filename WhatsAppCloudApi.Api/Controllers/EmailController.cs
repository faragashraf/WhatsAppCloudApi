using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Infrastructure.Data;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/email")]
[Authorize(Roles = "Admin")]
public sealed class EmailController : ApiControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IEmailQueueService _emailQueueService;
    private readonly ITenantContextAccessor _tenantContext;

    public EmailController(ApplicationDbContext db, IEmailQueueService emailQueueService, ITenantContextAccessor tenantContext)
    {
        _db = db;
        _emailQueueService = emailQueueService;
        _tenantContext = tenantContext;
    }

    private async Task<bool> IsSuperAdminAsync(CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        return await _db.CompanyUsers.AnyAsync(u => u.CompanyUserId == ctx.UserId && u.IsActive && u.IsSuperAdmin, ct);
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
        => ToActionResult(await _emailQueueService.GetDashboardAsync(_tenantContext.GetRequiredContext().CompanyId, "COMPANY", ct));

    [HttpGet("queue")]
    public async Task<IActionResult> GetQueue([FromQuery] EmailQueueQueryParams query, CancellationToken ct)
        => ToActionResult(await _emailQueueService.GetQueueAsync(_tenantContext.GetRequiredContext().CompanyId, "COMPANY", query, ct));

    [HttpPost("queue")]
    public async Task<IActionResult> QueueEmail([FromBody] QueueEmailRequest request, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        return ToActionResult(await _emailQueueService.QueueEmailAsync(ctx.CompanyId, ctx.UserId, "COMPANY", request, ct));
    }

    [HttpPost("queue/{id:long}/cancel")]
    public async Task<IActionResult> CancelQueuedEmail(long id, CancellationToken ct)
        => ToActionResult(await _emailQueueService.CancelQueueItemAsync(_tenantContext.GetRequiredContext().CompanyId, "COMPANY", id, ct));

    [HttpPost("queue/{id:long}/retry")]
    public async Task<IActionResult> RetryQueuedEmail(long id, CancellationToken ct)
        => ToActionResult(await _emailQueueService.RetryQueueItemAsync(_tenantContext.GetRequiredContext().CompanyId, "COMPANY", id, ct));

    [HttpGet("accounts")]
    public async Task<IActionResult> GetAccounts(CancellationToken ct)
        => ToActionResult(await _emailQueueService.GetAccountsAsync(_tenantContext.GetRequiredContext().CompanyId, "COMPANY", ct));

    [HttpPost("accounts")]
    public async Task<IActionResult> CreateAccount([FromBody] EmailAccountUpsertRequest request, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        return ToActionResult(await _emailQueueService.UpsertAccountAsync(ctx.CompanyId, ctx.UserId, "COMPANY", null, request, ct));
    }

    [HttpPut("accounts/{id:int}")]
    public async Task<IActionResult> UpdateAccount(int id, [FromBody] EmailAccountUpsertRequest request, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        return ToActionResult(await _emailQueueService.UpsertAccountAsync(ctx.CompanyId, ctx.UserId, "COMPANY", id, request, ct));
    }

    [HttpDelete("accounts/{id:int}")]
    public async Task<IActionResult> DeleteAccount(int id, CancellationToken ct)
        => ToActionResult(await _emailQueueService.DeleteAccountAsync(_tenantContext.GetRequiredContext().CompanyId, "COMPANY", id, ct));

    [HttpGet("rules")]
    public async Task<IActionResult> GetRules(CancellationToken ct)
    {
        if (!await IsSuperAdminAsync(ct))
        {
            return Forbid();
        }

        return ToActionResult(await _emailQueueService.GetRulesAsync(_tenantContext.GetRequiredContext().CompanyId, "COMPANY", ct));
    }

    [HttpPost("rules")]
    public async Task<IActionResult> CreateRule([FromBody] EmailNotificationRuleUpsertRequest request, CancellationToken ct)
    {
        if (!await IsSuperAdminAsync(ct))
        {
            return Forbid();
        }

        var ctx = _tenantContext.GetRequiredContext();
        return ToActionResult(await _emailQueueService.UpsertRuleAsync(ctx.CompanyId, ctx.UserId, "COMPANY", null, request, ct));
    }

    [HttpPut("rules/{id:long}")]
    public async Task<IActionResult> UpdateRule(long id, [FromBody] EmailNotificationRuleUpsertRequest request, CancellationToken ct)
    {
        if (!await IsSuperAdminAsync(ct))
        {
            return Forbid();
        }

        var ctx = _tenantContext.GetRequiredContext();
        return ToActionResult(await _emailQueueService.UpsertRuleAsync(ctx.CompanyId, ctx.UserId, "COMPANY", id, request, ct));
    }

    [HttpDelete("rules/{id:long}")]
    public async Task<IActionResult> DeleteRule(long id, CancellationToken ct)
    {
        if (!await IsSuperAdminAsync(ct))
        {
            return Forbid();
        }

        return ToActionResult(await _emailQueueService.DeleteRuleAsync(_tenantContext.GetRequiredContext().CompanyId, "COMPANY", id, ct));
    }
}
