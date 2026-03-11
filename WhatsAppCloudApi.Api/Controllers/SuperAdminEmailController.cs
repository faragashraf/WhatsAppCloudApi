using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Infrastructure.Data;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/super-admin/email")]
[Authorize]
public sealed class SuperAdminEmailController : ApiControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IEmailQueueService _emailQueueService;

    public SuperAdminEmailController(ApplicationDbContext db, IEmailQueueService emailQueueService)
    {
        _db = db;
        _emailQueueService = emailQueueService;
    }

    private async Task<bool> IsSuperAdmin(CancellationToken ct)
    {
        var userIdClaim = User.FindFirst("UserId")?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return false;
        }

        return await _db.CompanyUsers.AnyAsync(u => u.CompanyUserId == userId && u.IsSuperAdmin && u.IsActive, ct);
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("UserId")?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 0;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
    {
        if (!await IsSuperAdmin(ct))
        {
            return Forbid();
        }

        return ToActionResult(await _emailQueueService.GetDashboardAsync(null, "CRITICAL", ct));
    }

    [HttpGet("queue")]
    public async Task<IActionResult> GetQueue([FromQuery] EmailQueueQueryParams query, CancellationToken ct)
    {
        if (!await IsSuperAdmin(ct))
        {
            return Forbid();
        }

        return ToActionResult(await _emailQueueService.GetQueueAsync(null, "CRITICAL", query, ct));
    }

    [HttpPost("queue")]
    public async Task<IActionResult> QueueEmail([FromBody] QueueEmailRequest request, CancellationToken ct)
    {
        if (!await IsSuperAdmin(ct))
        {
            return Forbid();
        }

        return ToActionResult(await _emailQueueService.QueueEmailAsync(null, GetCurrentUserId(), "CRITICAL", request, ct));
    }

    [HttpPost("queue/{id:long}/cancel")]
    public async Task<IActionResult> CancelQueuedEmail(long id, CancellationToken ct)
    {
        if (!await IsSuperAdmin(ct))
        {
            return Forbid();
        }

        return ToActionResult(await _emailQueueService.CancelQueueItemAsync(null, "CRITICAL", id, ct));
    }

    [HttpPost("queue/{id:long}/retry")]
    public async Task<IActionResult> RetryQueuedEmail(long id, CancellationToken ct)
    {
        if (!await IsSuperAdmin(ct))
        {
            return Forbid();
        }

        return ToActionResult(await _emailQueueService.RetryQueueItemAsync(null, "CRITICAL", id, ct));
    }

    [HttpGet("accounts")]
    public async Task<IActionResult> GetAccounts(CancellationToken ct)
    {
        if (!await IsSuperAdmin(ct))
        {
            return Forbid();
        }

        return ToActionResult(await _emailQueueService.GetAccountsAsync(null, "CRITICAL", ct));
    }

    [HttpPost("accounts")]
    public async Task<IActionResult> CreateAccount([FromBody] EmailAccountUpsertRequest request, CancellationToken ct)
    {
        if (!await IsSuperAdmin(ct))
        {
            return Forbid();
        }

        return ToActionResult(await _emailQueueService.UpsertAccountAsync(null, GetCurrentUserId(), "CRITICAL", null, request, ct));
    }

    [HttpPut("accounts/{id:int}")]
    public async Task<IActionResult> UpdateAccount(int id, [FromBody] EmailAccountUpsertRequest request, CancellationToken ct)
    {
        if (!await IsSuperAdmin(ct))
        {
            return Forbid();
        }

        return ToActionResult(await _emailQueueService.UpsertAccountAsync(null, GetCurrentUserId(), "CRITICAL", id, request, ct));
    }

    [HttpDelete("accounts/{id:int}")]
    public async Task<IActionResult> DeleteAccount(int id, CancellationToken ct)
    {
        if (!await IsSuperAdmin(ct))
        {
            return Forbid();
        }

        return ToActionResult(await _emailQueueService.DeleteAccountAsync(null, "CRITICAL", id, ct));
    }

    [HttpGet("rules")]
    public async Task<IActionResult> GetRules(CancellationToken ct)
    {
        if (!await IsSuperAdmin(ct))
        {
            return Forbid();
        }

        return ToActionResult(await _emailQueueService.GetRulesAsync(null, "CRITICAL", ct));
    }

    [HttpPost("rules")]
    public async Task<IActionResult> CreateRule([FromBody] EmailNotificationRuleUpsertRequest request, CancellationToken ct)
    {
        if (!await IsSuperAdmin(ct))
        {
            return Forbid();
        }

        return ToActionResult(await _emailQueueService.UpsertRuleAsync(null, GetCurrentUserId(), "CRITICAL", null, request, ct));
    }

    [HttpPut("rules/{id:long}")]
    public async Task<IActionResult> UpdateRule(long id, [FromBody] EmailNotificationRuleUpsertRequest request, CancellationToken ct)
    {
        if (!await IsSuperAdmin(ct))
        {
            return Forbid();
        }

        return ToActionResult(await _emailQueueService.UpsertRuleAsync(null, GetCurrentUserId(), "CRITICAL", id, request, ct));
    }

    [HttpDelete("rules/{id:long}")]
    public async Task<IActionResult> DeleteRule(long id, CancellationToken ct)
    {
        if (!await IsSuperAdmin(ct))
        {
            return Forbid();
        }

        return ToActionResult(await _emailQueueService.DeleteRuleAsync(null, "CRITICAL", id, ct));
    }
}
