using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Infrastructure.Data;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/messages")]
[Authorize]
public sealed class MessagesController : ApiControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantContextAccessor _tenantContext;

    public MessagesController(ApplicationDbContext db, ITenantContextAccessor tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Get paged message logs for the current company.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMessages(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? type = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var ctx = _tenantContext.GetRequiredContext();
        if (!await CanViewMessagesAsync(ctx.CompanyId, ctx.UserId, ctx.Role, cancellationToken))
        {
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Messages
            .Where(m => m.CompanyId == ctx.CompanyId)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(m => m.Status == status);

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(m => m.MessageType == type);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(m => m.ToNumber.Contains(search) || m.MessageBody.Contains(search));

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(m => m.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new
            {
                m.MessageId,
                m.CompanyId,
                m.WhatsAppPhoneNumberId,
                m.ToNumber,
                m.MessageType,
                m.MessageBody,
                m.Status,
                m.ExternalMessageId,
                m.FailureReason,
                m.CreatedAtUtc,
                m.UpdatedAtUtc,
            })
            .ToListAsync(cancellationToken);

        var result = new
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
            HasNext = page * pageSize < totalCount,
            HasPrevious = page > 1,
        };

        return Ok(ApiResponse<object>.Ok(result));
    }

    private async Task<bool> CanViewMessagesAsync(int companyId, int userId, string role, CancellationToken ct)
    {
        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var user = await _db.CompanyUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.CompanyUserId == userId && u.CompanyId == companyId && u.IsActive, ct);
        return user?.EffectivePermissions.MessagesView ?? false;
    }
}
